using Gemini.Models;
using Gemini.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using MiniExcelLibs;
using S7.Net;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.PortableExecutable;
using System.Collections.Concurrent;
using System.Threading;


//using OfficeOpenXml.FormulaParsing.Excel.Functions.DateTime;
using System.Text.Json;
using System.Transactions;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

//using static OfficeOpenXml.ExcelErrorValue;
using DateTime = System.DateTime;

namespace Gemini.Db
{
    internal sealed partial class Db
    {
        /// <summary>
        /// Initialsiert die Datenbankschreibfunktion, indem sie alle Tag-Namen mit Log-Flag aus der Datenbank lädt und als Client im PlcTagManager anmeldet.
        /// </summary>
        internal static async void InitiateDbWriting()
        {
            //Lade alle Tag-Namen mit Log-Flog aus der Datenbank
            List<JsonTag> dummyData = [];
            List<Tag> tags = GetDbTagNames(DateTime.UtcNow);
            tags.ForEach(tag =>
            {
                if (tag.ChartFlag == true)
                    dummyData.Add(new JsonTag(tag.TagName, tag.TagValue, DateTime.UtcNow));
            });

            var dbClientId = Guid.NewGuid(); //Datenbank wie jeden anderen Client im PlcTagManager anmelden.
            PlcTagManager.Instance.DataBaseClientIdentifier = dbClientId; //ID des Datenbank-Clients im PlcTagManager speichern

            DbLogInfo($"Datenbank als lokalen Client mit {dummyData.Count} Tags anmelden..");
#if DEBUG
            //Console.WriteLine($"Die Datenbank loggt sich ein als Client {dbClientId}");
#endif

            static async Task SendDbUpdateCallback(Models.JsonTag[] tagsToSend)
            {
                // Da die ID in dieser Funktion verwendet wird, 'captured' (erfasst) sie die 
                // lokale Variable 'dbClientId'. Trotzdem ist der Code idiomatisch und sauberer.
                await SendDbUpdateAsync(tagsToSend);
            }

            PlcTagManager.Instance.AddOrUpdateClient(dbClientId, System.Net.IPAddress.Loopback, [.. dummyData], SendDbUpdateCallback);

            // Sicherstellen, dass der Hintergrund-Writer läuft
            EnsureBackgroundWriterRunning();
        }

        #region Background DB Writer

        // Queue statt List+Lock
        private static readonly ConcurrentQueue<JsonTag> TagsWriteQueue = new();
        private static readonly SemaphoreSlim _queueSignal = new(0);
        private static CancellationTokenSource? _writerCts;
        private static Task? _writerTask;
        private static readonly Lock _writerLock = new();

        // Konfiguration
        const int TagsWriteBufferMax = 500;
        private const int WriterMaxBatch = 1000; // max. Tags pro DB-Schreibvorgang (tunable)
        private static readonly TimeSpan WriterIdleDelay = TimeSpan.FromMilliseconds(500); // Sammelzeitfenster (optional)

        // Aufruf: beim Start (z.B. in InitiateDbWriting) sicherstellen, dass Writer läuft:
        private static void EnsureBackgroundWriterRunning()
        {
            lock (_writerLock)
            {
                if (_writerTask != null && !_writerTask.IsCompleted) return;
                _writerCts = new CancellationTokenSource();
                _writerTask = Task.Run(() => BackgroundWriterLoop(_writerCts.Token));
            }
        }

        // Public: Stoppen und Flush (z.B. beim Anwendungsshutdown)
        internal static async Task StopBackgroundWriterAsync(TimeSpan timeout)
        {
            Task? task;
            lock (_writerLock)
            {
                if (_writerCts == null) return;
                _writerCts.Cancel();
                task = _writerTask;
            }

            if (task != null)
            {
                try { await Task.WhenAny(task, Task.Delay(timeout)); } catch { }
            }

            // Flush restliche Items synchron
            var remaining = new List<JsonTag>();
            while (TagsWriteQueue.TryDequeue(out var jt)) remaining.Add(jt);
            if (remaining.Count > 0)
            {
                try { InsertTagsBulk([.. remaining]); } catch { }
            }
        }

        // Producer: Enqueue und Signal
        internal static async Task SendDbUpdateAsync(JsonTag[] tagsToSend)
        {
            if (tagsToSend == null || tagsToSend.Length == 0) return;

            // Ensure writer started (idempotent)
            EnsureBackgroundWriterRunning();

            foreach (var t in tagsToSend)
                TagsWriteQueue.Enqueue(t);

            // Signal: einmalig oder per Anzahl; hier per Anzahl
            try
            {
                _queueSignal.Release(tagsToSend.Length);
            }
            catch (SemaphoreFullException)
            {
                // safe fallback: wenn Semaphore voll ist, releasen wir einmal
                try { _queueSignal.Release(); } catch { }
            }
        }

        // Hintergrund-Consumer
        private static async Task BackgroundWriterLoop(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        // Warte auf mindestens ein Item (oder Cancellation)
                        await _queueSignal.WaitAsync(ct);
                    }
                    catch (OperationCanceledException) { break; }

                    // Nach erstem Signal: Sammle bis zu WriterMaxBatch Elemente (oder TagsWriteBufferMax) schnell zusammen
                    var batch = new List<JsonTag>(Math.Min(WriterMaxBatch, TagsWriteBufferMax));
                    while (batch.Count < WriterMaxBatch && TagsWriteQueue.TryDequeue(out var jt))
                    {
                        batch.Add(jt);
                        // optional: wenn wir noch sehr wenige Items haben, kurz warten um weitere anzuhäufen
                        if (batch.Count < TagsWriteBufferMax && TagsWriteQueue.IsEmpty)
                        {
                            // Kurzes Sammel-Delay, erlaubt weitere Producer beizusteuern
                            await Task.Delay(WriterIdleDelay, ct).ContinueWith(_ => { }, TaskScheduler.Default);
                        }
                    }

                    if (batch.Count == 0) continue;

                    try
                    {
                        // Schreibe Batch in die DB (InsertTagsBulk ist bereits vorhanden)
                        InsertTagsBulk([.. batch]);
                    }
                    catch (Exception ex)
                    {
                        // Fehlerbehandlung: loggen, evtl. zurück in Queue (vorsichtig)
                        DbLogInfo($"Fehler beim Hintergrund-DB-Write: {ex}");
                        // Optional: fallback — versuche einzelne Einträge erneut oder verwerfe
                    }

                    // Wenn noch Items in der Queue sind, loop direkt weiter (kein Warte-Delay)
                    while (!ct.IsCancellationRequested && !TagsWriteQueue.IsEmpty)
                    {
                        var more = new List<JsonTag>(WriterMaxBatch);
                        while (more.Count < WriterMaxBatch && TagsWriteQueue.TryDequeue(out var jt2))
                            more.Add(jt2);
                        if (more.Count == 0) break;
                        try { InsertTagsBulk([.. more]); } catch { DbLogInfo("Fehler beim Hintergrund-DB-Write (2)."); }
                    }
                }
            }
            catch (Exception ex)
            {
                DbLogInfo("BackgroundWriterLoop terminated unexpectedly: " + ex);
            }
        }

        #endregion

        /// <summary>
        /// Schwellwert für die Anzahl der geänderten Tags, die im Puffer gehalten werden, bevor sie in die Datenbank geschrieben werden.
        /// </summary>
        //const int TagsWriteBufferMaxOld = 100;

        /// <summary>
        /// Puffer für geänderte Tags, die in die Datenbank geschrieben werden sollen. 
        /// Sobald die Anzahl der Tags im Puffer den Schwellenwert 'TagsWriteBufferMax' erreicht, werden sie in die Datenbank geschrieben.
        /// Soll die Anzahl der Schreibvorgänge reduzieren und die Performance verbessern, 
        /// auf Kosten eines möglichen Datenverlusts bei einem Absturz (je nach Schwellenwert).
        /// </summary>
       // private static List<JsonTag> TagsWriteBufferOld { get; set; } = [];
        
        /// <summary>
        /// Returns a list of database file paths for each day in the specified date range.
        /// </summary>
        /// <param name="startUtcDate">The start date of the range, in Coordinated Universal Time (UTC). The search includes this date.</param>
        /// <param name="endUtcDate">The end date of the range, in Coordinated Universal Time (UTC). The search includes this date.</param>
        /// <returns>A list of strings containing the file paths for each day's database within the specified date range. The
        /// list is empty if the range is invalid or contains no days.</returns>
        internal static List<string> GetDatabasePaths(DateTime startUtcDate, DateTime endUtcDate)
        {
            List<string> dbFilePaths = [];
            for (DateTime day = startUtcDate.Date; day.Date <= endUtcDate.Date; day = day.AddDays(1))
            {
                string dbPath = GetDayDbPath(day);
#if DEBUG
                Console.WriteLine("Finde Pfad " + dbPath);
#endif
                dbFilePaths.Add(dbPath);
            }

            return dbFilePaths;
        }


        /// <summary>
        /// Liest alle Tag-Namen aus der Tagesdatenbank mit dem Datum <date>. Wenn keine Tags gefunden werden, wird für max. <counter> Tage zurückgegangen, um Tag-Namen zu finden.
        /// </summary>
        /// <param name="date"></param>
        /// <param name="lookBackDays"></param>
        /// <returns>Dictionary <TagName, TagComment></returns>
        public static List<Tag> GetDbTagNames(DateTime date, int lookBackDays = 9)
        {
            if (lookBackDays < 0 || lookBackDays > 9)
                lookBackDays = 9; //Sicherheitshalber auf 9 begrenzen, da max. 10 Datenbanken (inkl. aktueller) angehängt werden können.

            List<Tag> tags = [];

            lock (_dbLock)
            {
                using var connection = new SqliteConnection(DayDbSource);
                connection.Open();
                var command = connection.CreateCommand();

                while (lookBackDays-- > 0)
                {
                    string dbPath = GetDayDbPath(date);
                    date = date.AddDays(-1);

                    if (!File.Exists(dbPath))
                    {
#if DEBUG                        
                        Db.DbLogInfo($"Tagestabelle: Datei {Path.GetFileName(dbPath)} nicht gefunden.");
#endif
                        continue;
                    }

                    //Console.WriteLine($"Tagestabelle: Datei {dbPath} gefunden.");

                    if (date.Date == DateTime.UtcNow.Date)
                        command.CommandText = @"SELECT Name, Comment, ChartFlag FROM Tag;";
                    else
                        command.CommandText = $@"
                        ATTACH DATABASE '{dbPath}' AS old_db; 
                        SELECT Name, Comment, ChartFlag FROM Tag; 
                        DETACH DATABASE old_db; ";

                    using var reader = command.ExecuteReader(); // maximal 10 Databases dürfen attached sein!
                    while (reader.Read())
                    {
                        string tagName = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
                        string tagComment = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        bool chartFlag = reader.GetBoolean(2);
                        //Console.WriteLine($"Tagestabelle: Gefundener TagName {tagName} mit Kommentar {tagComment}.");
                        tags.Add(new Tag(tagName, tagComment, null, chartFlag));
                    }

                    if (tags.Count > 0)
                        break;
                }
                connection.Dispose();
            }
            return tags;
        }

        /// <summary>
        /// Findet TagNames anhand des Kommentars in der Datenbank. 
        /// (Wenn der TagName schon als Kommentar übergeben wurde, unverändert ausgeben.)
        /// </summary>
        /// <param name="comments">Liste von Tag-Kommentarten</param>
        /// <returns>Liste von TagNames</returns>
        internal static async Task<Dictionary<string, string>> GetTagNamesFromComments(string[] comments)
        {
            Dictionary<string, string> tags = [];

            lock (_dbLock)
            {
                using var connection = new SqliteConnection(DayDbSource);
                connection.Open();
                var command = connection.CreateCommand();

                command.CommandText = @"
                SELECT Name FROM TAG WHERE Comment = @Comment OR Name = @Comment;
            ";

                var commentParam = command.Parameters.Add("@Comment", SqliteType.Text);

                foreach (var comment in comments)
                {
                    commentParam.Value = comment;

                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        string tagName = reader.GetString(0);
                        tags.Add(tagName, comment);
                    }
                }
                connection.Dispose();
            }

            return tags;
        }

        /// <summary>
        /// Gibt Alias-Namen und Pfade aller existierenden Datenbanken zwischen start und end aus
        /// </summary>
        /// <param name="startUtc">Start LocalTime</param>
        /// <param name="endUtc">End LacalTime</param>
        /// <returns>Dictionary <DataBaseName, DataBasePath></returns>
        private static Dictionary<DateTime, string> GetDataBasePathsForQuery(System.DateTime startUtc, System.DateTime endUtc)
        {
            Dictionary<DateTime, string> dataBases = []; //Name, Pfad

            for (DateTime day = startUtc.Date; day.Date <= endUtc.Date; day = day.AddDays(1))
            {
                string dbPath = GetDayDbPath(day);
                if (!File.Exists(dbPath))
                {
#if DEBUG
                    // Console.WriteLine($"✖ Datenbank {dbPath} für Tag {day:yyyy-MM-dd} existiert nicht.");
#endif
                    continue;
                }
                //Console.WriteLine($"✔ Datenbank |{dbPath}| für Tag {day:yyyy-MM-dd} existiert.");
                dataBases[day] = dbPath;
            }

            return dataBases;
        }

        /// <summary>
        /// Retrieves time series data for the specified tag names within the given date range, aggregated by the
        /// specified interval.
        /// </summary>
        /// <remarks>The method aggregates data based on the specified interval, if supported. Not all
        /// interval values may be implemented. The method is thread-safe and can be called concurrently. Data is
        /// retrieved from one or more underlying databases, and only unique tag names are processed.</remarks>
        /// <param name="tagNames">An array of tag names for which to retrieve data. Duplicate tag names are ignored.</param>
        /// <param name="startUtc">The start date and time of the data retrieval range. Only data points with timestamps greater than or equal
        /// to this value are included.</param>
        /// <param name="endUtc">The end date and time of the data retrieval range. Only data points with timestamps less than or equal to
        /// this value are included.</param>
        /// <param name="interval">The aggregation interval to use for grouping data points. If not specified, no aggregation is applied. Only
        /// certain intervals are supported.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains an array of JsonTag objects,
        /// each representing a data point for a tag within the specified range. The array is empty if no data is found.</returns>
        public static async Task<JsonTag[]> GetDataSet(string[] tagNames, System.DateTime startUtc, System.DateTime endUtc, Gemini.DynContent.MiniExcel.Interval interval = 0)
        {
            List<JsonTag> items = [];

            int roundSeconds = 0; // Aggregatfunktion für die Zeiten in der Datenabfrage
            string tableName = "Data";

            tagNames = [.. tagNames.Distinct()]; //Doppelte TagNames entfernen

            switch (interval)
            {
                //case DynContent.MiniExcel.Interval.Sekunde:
                //    break;
                case DynContent.MiniExcel.Interval.Minute:
                    tableName = "DataMinute";
                    roundSeconds = 60;
                    break;
                case DynContent.MiniExcel.Interval.Viertelstunde:
                    roundSeconds = 900;
                    break;
                case DynContent.MiniExcel.Interval.Stunde:
                    roundSeconds = 3600;
                    break;
                case DynContent.MiniExcel.Interval.Tag:
                    roundSeconds = 86400;
                    break;
                case DynContent.MiniExcel.Interval.Monat:
                    //nicht implementiert
                    break;
                case DynContent.MiniExcel.Interval.Jahr:
                    //nicht implementiert
                    break;
                    //default:
                    //    break;
            }

#if DEBUG
            Console.WriteLine($"Zeit Aggregat {interval} mit {roundSeconds} Sekunden.");
#endif
            #region Datenbanken
            //_ = GetAttachedDatabases(true); //Alle angeschlossenen Datenbanken ausdocken (eig. nur nötig wenn vorhergehende Transaktion unvollständig war
            Dictionary<DateTime, string> dataBases = GetDataBasePathsForQuery(startUtc, endUtc);
            #endregion

            // lock (_dbLock)
            if (true) //Lock hier nicht nötig, da in GetDataBasePathsForQuery() bereits geprüft wird, welche Datenbanken existieren, und die Datenbankverbindung in diesem Block nur lesend verwendet wird.
                      //Solange keine Schreibvorgänge parallel stattfinden, sollte es hier zu keinen Konflikten kommen.                    

            {
                #region SQLite Verbindung
                using var connection = new SqliteConnection(DayDbSource);
                connection.Open();
                //await using var transaction = connection.BeginTransaction(); Transaction hier nicht gut, weil Datenbanken gelockt werden könnten.
                var command = connection.CreateCommand();

                var nameParam = command.Parameters.Add("@TagName", SqliteType.Text);
                var startParam = command.Parameters.Add("@Start", SqliteType.Text);
                var endParam = command.Parameters.Add("@End", SqliteType.Text);
                var roundParam = command.Parameters.Add("@Round", SqliteType.Integer);
                roundParam.Value = roundSeconds;
                #endregion

                try
                {

                    #region in Blöcken von 9 lesen, da max 10 Datenbanken angehängt sein dürfen
                    int pos = 0;
                    while (pos <= dataBases.Count - 1)
                    {
                        int steps = Math.Min(9, dataBases.Count - pos);
                        var dbChunk = new Dictionary<DateTime, string>(dataBases.Skip(pos).Take(steps)); // Math.Min(attach.Count, 9))
                        pos += steps;
                        //Console.WriteLine($"GetDataSet2() Lese Datenbanken im Bereich {pos} für {steps} Steps (bis insgesamt {dataBases.Count})");

                        List<string> attach = [];
                        List<string> query = []; // "PRAGMA wal_checkpoint(FULL);"//konsolidiert die Write-Ahead-Log-Datei vor der Abfrage
                        List<string> detach = [];

                        #region Querys zusammenschrauben
                        foreach (var day in dbChunk.Keys)
                        {
                            //Console.WriteLine($"✔ Datenbank für Tag {day:yyyy-MM-dd} wird abgefragt.");
                            string dbName = $"db{day.Year:00}{day.Month:00}{day.Day:00}";


                            if (day.Date == DateTime.UtcNow.Date)
                            {
                                //if (roundSeconds > 1)
                                //    query.Add($" SELECT datetime(((strftime('%s', Time) + @Round - 1) / @Round) * @Round, 'unixepoch') AS Time, TagValue FROM main.Data WHERE TagId = (SELECT Id FROM main.Tag WHERE Name = @TagName) AND Time BETWEEN @Start AND @End; ");
                                //else
                                query.Add($" SELECT Time, TagValue FROM main.{tableName} WHERE TagId = (SELECT Id FROM main.Tag WHERE Name = @TagName) AND Time BETWEEN @Start AND @End ");
                            }
                            else
                            {
                                attach.Add($"ATTACH DATABASE '{dbChunk[day]}' AS '{dbName}';");
                                //if (roundSeconds > 1)
                                //    query.Add($" SELECT datetime(((strftime('%s', Time) + @Round - 1) / @Round) * @Round, 'unixepoch') AS Time, TagValue FROM {dbName}.Data WHERE TagId = (SELECT Id FROM {dbName}.Tag WHERE Name = @TagName) AND Time BETWEEN @Start AND @End ");
                                //else
                                query.Add($" SELECT Time, TagValue FROM {dbName}.{tableName} WHERE TagId = (SELECT Id FROM {dbName}.Tag WHERE Name = @TagName) AND Time BETWEEN @Start AND @End ");
                                detach.Add($"DETACH DATABASE '{dbName}';");
                            }
                        }
                        #endregion

                        #region Datenbanken anhängen
                        command.CommandText = string.Join(' ', attach);
                        //Console.WriteLine(command.CommandText+ "\r\n\r\n");
                        int result = command.ExecuteNonQuery();

                        //Console.WriteLine($"ExecuteNonQueryAsync() = {result}\r\n" + string.Join('|', await GetAttachedDatabases()));
                        #endregion

                        #region Abfrage ausführen
                        command.CommandText = string.Join(" UNION ", query) + " ORDER BY Time; ";
                        //if (roundSeconds > 1)
                        //    command.CommandText += "GROUP BY Time HAVING Time = MAX(Time) ORDER BY Time ";

#if DEBUG
                        Console.WriteLine(command.CommandText + "\r\n\r\n");
#endif
                        foreach (var tagName in tagNames)
                        {
                            if (tagName?.Length < 1)
                                continue; //leerer TagName

                            nameParam.Value = tagName;
                            startParam.Value = startUtc.ToString("yyyy-MM-dd HH:mm:ss");
                            endParam.Value = endUtc.ToString("yyyy-MM-dd HH:mm:ss");

                            //Console.WriteLine($"GetDataSet() Abfrage '{nameParam.Value}' von '{startParam.Value}' bis '{endParam.Value}'");

                            using var reader2 = command.ExecuteReader();
                            while (reader2.Read())
                            {
                                string v = reader2.GetString(1);
                                ///Console.WriteLine($"Gelesener Wert für Tag {tagName}: {v}");
                                object? value = null;

                                if (double.TryParse(v, out double floatValue))
                                    value = floatValue;
                                else if (Int16.TryParse(v, out Int16 intValue))
                                    value = intValue;
                                else if (bool.TryParse(v, out bool boolValue))
                                    value = boolValue;

                                DateTime t = reader2.GetDateTime(0).ToLocalTime();

                                items.Add(new JsonTag(tagName!, value, t));
                            }
                        }
                        #endregion

                        #region Datenbanken aushängen
                        command.CommandText = string.Join(' ', detach);
                        //Console.WriteLine(command.CommandText + "\r\n\r\n");
                        await command.ExecuteNonQueryAsync();


                        #endregion
                    }
                    #endregion

                    //await transaction.CommitAsync(); Transaction hier nicht gut, weil Datenbanken gelockt werden könnten.
                }
                catch (Exception ex)
                {
                    // Bei einem Fehler: Rollback der Transaktion
                    //transaction.Rollback(); Transaction hier nicht gut, weil Datenbanken gelockt werden könnten.
                    Db.DbLogInfo($"Fehler bei der Abfrage in GetDataSet(). {ex}");
                }
                //finally
                //{
                //    //Alle Datenbanken wirklich wieder lösen
                //    connection.DisposeAsync();
                //}
            }

            DetachAllDataBases();
            return [.. items];

        }

        /// <summary>
        /// Detaches all attached SQLite databases from the current database connection, except for the main database.
        /// </summary>
        /// <remarks>This method removes all additional databases that have been attached to the primary
        /// SQLite connection. The main database remains attached. Use this method to ensure that only the main database
        /// is connected, for example, before performing operations that require exclusive access.</remarks>
        private static void DetachAllDataBases()
        {
            string detach = string.Empty;

            lock (_dbLock)
            {
                using var connection = new SqliteConnection(DayDbSource);
                connection.Open();
                //await using var transaction = connection.BeginTransaction(); Transaction hier nicht gut, weil Datenbanken gelockt werden könnten.
                var command = connection.CreateCommand();
                command.CommandText = "SELECT name FROM pragma_database_list;";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string dbName = reader.GetString(0);
                    if (dbName != "main")
                        detach += $"DETACH DATABASE '{dbName}';";
                }
                connection.Close();

                command.CommandText = detach;
                connection.Open();
                command.ExecuteNonQuery();

            }
        }

        internal static void TagUpdate(string tagName, string? tagComm, bool tagChart)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(DayDbSource);
                connection.Open();
                var command = connection.CreateCommand();

                var nameParam = command.Parameters.Add("@TagName", SqliteType.Text);
                var commentParam = command.Parameters.Add("@TagComment", SqliteType.Text);
                var chartParam = command.Parameters.Add("@ChartFlag", SqliteType.Text);

                nameParam.Value = tagName;
                commentParam.Value = tagComm;
                chartParam.Value = tagChart;

                command.CommandText = @"UPDATE Tag SET Comment = @TagComment, ChartFlag = @ChartFlag WHERE Name = @TagName;";
                int result = command.ExecuteNonQuery();
#if DEBUG
                DbLogInfo($"Beim Ändern von {tagName} wurden {result} Zeilen in der Datenbank geändert.");
#endif
            }
        }

        /// <summary>
        /// Schreibe die Tags in die Datenbank
        /// </summary>
        /// <param name="jsonTags">Tags zum Schreiben in die Datenbank</param>
        internal static async void InsertTagsBulk(JsonTag[] jsonTags)
        {
            CreateDayDatabaseAsync();
            //string connectionString = "Data Source=" + GetDayDbPath(DateTime.UtcNow); //DayDbSource wird bei Aufruf nicht aktualisiert, daher hier direkt den Pfad holen

            //TEST Wird nach Tagessprung in die richtige Datenbank geschrieben? --> JA
            //if (DateTime.UtcNow.Hour < 1)
            //    Db.DbLogInfo($"InsertTagsBulk() {DateTime.UtcNow:t} mit {connectionString}");

            lock (_dbLock)
            {
                using var connection = new SqliteConnection(DayDbSource);
                connection.Open();

                try
                {
                    var command = connection.CreateCommand();

                    command.CommandText =
                        @$"
                      INSERT OR IGNORE INTO Tag (Name) VALUES (@TagName); 
                      INSERT INTO Data (Time, TagId, TagValue) VALUES (
                        @TagTime
                        ,(SELECT Id FROM Tag WHERE Name = @TagName)
                        ,@TagValue
                      );";

                    var nameParam = command.Parameters.Add("@TagName", SqliteType.Text);
                    var valueParam = command.Parameters.Add("@TagValue", SqliteType.Blob);
                    var timeParam = command.Parameters.Add("@TagTime", SqliteType.Text);

                    foreach (var tag in jsonTags)
                    {
                        if (tag?.N is null) continue;

                        nameParam.Value = tag.N;
                        valueParam.Value = tag.V;
                        timeParam.Value = tag.T.ToString("yyyy-MM-dd HH:mm:ss");
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    Db.DbLogInfo($"Fehler beim Batch-Insert. {ex}");
                }
            }
        }

        internal static async void InsertTagNamesBulk(JsonTag[] jsonTags)
        {
            CreateDayDatabaseAsync();

            lock (_dbLock)
            {
                using var connection = new SqliteConnection(DayDbSource);
                connection.Open();

                try
                {
                    var command = connection.CreateCommand();
                    command.CommandText = "INSERT OR IGNORE INTO Tag (Name) VALUES (@TagName);";

                    var nameParam = command.Parameters.Add("@TagName", SqliteType.Text);

                    foreach (var tag in jsonTags)
                    {
                        nameParam.Value = tag.N;
                        command.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    Db.DbLogInfo($"Fehler beim TagName-Batch-Insert. {ex}");
                }

            }
        }


        internal static int WriteTag(string tagName, string tagVal, string oldVal, string username)
        {
            if (tagName.Contains('X'))
                switch (oldVal)
                {
                    case "☐":
                        oldVal = "0";
                        break;
                    case "☒":
                        oldVal = "1";
                        break;
                }

            try
            {
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(DayDbSource);
                    connection.Open();
                    var command = connection.CreateCommand();

                    command.Parameters.Add("@TagName", SqliteType.Text).Value = tagName;
                    command.Parameters.Add("@TagValue", SqliteType.Blob).Value = tagVal;
                    command.Parameters.Add("@OldValue", SqliteType.Blob).Value = oldVal;
                    command.Parameters.Add("@User", SqliteType.Text).Value = username;

                    command.CommandText = @$"
                      INSERT OR IGNORE INTO Tag (Name) VALUES (@TagName); 
                      INSERT INTO Setpoint (TagId, TagValue, OldValue, User) VALUES (                        
                        (SELECT Id FROM Tag WHERE Name = @TagName)
                        ,@TagValue
                        ,@OldValue
                        ,@User
                      );";

#if DEBUG
                    Console.WriteLine($"WriteTag: Schreibe Tag {tagName} mit Wert {tagVal} (alt {oldVal}) von Benutzer {username} in die Datenbank.");
#endif
                    return command.ExecuteNonQuery();
                }
            }
            catch
            {
                throw;
            }
        }

        //Auflistung der Tag-Änderungen (Setpoints) zwischen zwei Zeitpunkten mit User, altem und neuem Wert, TagName und Kommentar. Nützlich für die Anzeige von Änderungen in einem Änderungsprotokoll.
        internal static List<TagAltered> SelectTagAlterations(DateTime startUtc, DateTime endUtc)
        {
            //Ausgabe: Zeit|User|TagName|TagComment|NewValue|OldValue
            /* CREATE TABLE IF NOT EXISTS Setpoint (                         
                    Time TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, 
                    TagId INT NOT NULL,
                    TagValue NUMERIC,
                    OldValue NUMERIC,
                    User TEXT,                */

            List<TagAltered> alteredTags = [];

            Dictionary<DateTime, string> dataBases = GetDataBasePathsForQuery(startUtc, endUtc);
#if DEBUG
            Console.WriteLine($"Sollwertänderungen in {dataBases.Count} Datenbanken suchen..");
#endif
            using var connection = new SqliteConnection(DayDbSource); //ToDo: mehrere Datenbanken ATTACH
            connection.Open();
            var command = connection.CreateCommand();
            try
            {
                int pos = 0;
                while (pos <= dataBases.Count - 1)
                {
                    int steps = Math.Min(9, dataBases.Count - pos); //Max. Anzahl angehängter Datenbanken ist 10 (inkl. main), daher in Blöcken von 9 vorgehen
                    var dbChunk = new Dictionary<DateTime, string>(dataBases.Skip(pos).Take(steps));
                    pos += steps;

                    List<string> attach = [];
                    List<string> query = []; // "PRAGMA wal_checkpoint(FULL);"//konsolidiert die Write-Ahead-Log-Datei vor der Abfrage
                    List<string> detach = [];

                    foreach (var day in dbChunk.Keys)
                    {
                        if (day.Date == DateTime.UtcNow.Date)
                            //aktuelle Datenbank muss nicht angehängt werden
                            query.Add($"SELECT Time, User, (SELECT Name FROM main.Tag WHERE Id = TagId) AS TagName, (SELECT Comment FROM main.Tag WHERE Id = TagId) AS TagComment, TagValue, OldValue FROM main.Setpoint WHERE Time BETWEEN @Start AND @End ");
                        else
                        {
                            string dbName = $"db{day.Year:00}{day.Month:00}{day.Day:00}";
                            attach.Add($"ATTACH DATABASE '{dbChunk[day]}' AS '{dbName}';");
                            query.Add($" SELECT Time, User, (SELECT Name FROM {dbName}.Tag WHERE Id = TagId) AS TagName, (SELECT Comment FROM {dbName}.Tag WHERE Id = TagId) AS TagComment, TagValue, OldValue FROM {dbName}.Setpoint WHERE Time BETWEEN @Start AND @End ");
                            detach.Add($"DETACH DATABASE '{dbName}';");
                        }
                    }

                    #region Datenbanken anhängen
                    command.CommandText = string.Join(' ', attach);
                    //Console.WriteLine($"SelectTagAlterations() ATTACH\r\n{string.Join(Environment.NewLine, attach)}");

                    int result = command.ExecuteNonQuery();
                    #endregion

                    #region Abfrage ausführen
                    var startParam = command.Parameters.Add("@Start", SqliteType.Text);
                    var endParam = command.Parameters.Add("@End", SqliteType.Text);
                    startParam.Value = startUtc.ToString("yyyy-MM-dd HH:mm:ss");
                    endParam.Value = endUtc.ToString("yyyy-MM-dd HH:mm:ss");

                    command.CommandText = string.Join(" UNION ", query) + "ORDER BY Time DESC; ";
#if DEBUG
                    Console.WriteLine($"SelectTagAlterations()\r\n{string.Join("\r\nUNION ", query) + "\r\nORDER BY Time DESC; "}");
                    Console.WriteLine($"{startParam.ParameterName}={startParam.Value}");
                    Console.WriteLine($"{endParam.ParameterName}={endParam.Value}");
#endif
                    using var reader = command.ExecuteReader();
                    try
                    {
                        while (reader.Read())
                        {
                            //Zeit | User | TagName | TagComment | NewValue | OldValue
                            string timeStr = reader.GetString(0);
                            _ = DateTime.TryParse(timeStr, out DateTime time);// ? time : DateTime.MinValue;

                            string user = reader.GetString(1);
                            string tagName = reader.GetString(2);
                            string tagComment = reader.IsDBNull(3) ? tagName : reader.GetString(3);
                            object newValue = reader.GetValue(4);
                            object oldValue = reader.GetValue(5);

                            if (tagName.Contains('X'))
                            {   //object as int siehe https://stackoverflow.com/a/745204/22035462                       
                                newValue = newValue as Int64? > (Int64)0 ? "☒" : "☐";
                                oldValue = oldValue as Int64? > (Int64)0 ? "☒" : "☐";
                            }

                            var tag = new TagAltered(time, tagName, tagComment, newValue, oldValue, user);
#if DEBUG
                            Console.WriteLine($"Gefundene Änderung: {tag.Timestamp} | {tag.User} | {tag.TagName} | {tag.TagComment} | {tag.NewValue} | {tag.OldValue}");
#endif
                            alteredTags.Add(tag);
                        }
                    }
                    catch (Exception ex) { Db.DbLogError("SelectTagAlterations() " + ex); }
                    finally { reader.Close(); }

                    #endregion

                    #region Datenbanken aushängen
                    command.CommandText = string.Join(' ', detach);
#if DEBUG
                    Console.WriteLine($"SelectTagAlterations()\r\n{command.CommandText}");
#endif
                    command.ExecuteNonQueryAsync();
                    #endregion
                }
            }
            catch (Exception ex)
            {
                // Bei einem Fehler: Rollback der Transaktion
                //transaction.Rollback(); Transaction hier nicht gut, weil Datenbanken gelockt werden könnten.
                Db.DbLogError($"Fehler bei der Abfrage in SelectTagAlteration(). {ex}");
            }
            finally
            {
                //Alle Datenbanken wirklich wieder lösen
                connection.Dispose();

                DetachAllDataBases();
            }

            return alteredTags;
        }

    }
}

