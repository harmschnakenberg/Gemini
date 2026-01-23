using Gemini.Models;
using Gemini.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using MiniExcelLibs;
using S7.Net;

//using OfficeOpenXml.FormulaParsing.Excel.Functions.DateTime;
using System.Text.Json;
using System.Transactions;
using System.Xml.Linq;
//using static OfficeOpenXml.ExcelErrorValue;
using DateTime = System.DateTime;

namespace Gemini.Db
{
    internal partial class Db
    {
        internal static async void InitiateDbWriting()
        {
            //Lade alle Tag-Namen mit Log-Flog aus der Datenbank
            List<JsonTag> dummyData = [];
            List<Tag> tags = await GetDbTagNames(DateTime.UtcNow);
            tags.ForEach(tag => {
                if (tag.ChartFlag == true)
                    dummyData.Add(new JsonTag(tag.TagName, tag.TagValue, DateTime.UtcNow));
                }); 
           
            var dbClientId = Guid.NewGuid(); //Datenbank wie jeden anderen Client im PlcTagManager anmelden.
#if DEBUG
            //Console.WriteLine($"Die Datenbank loggt sich ein als Client {dbClientId}");
#endif

            async Task SendDbUpdateCallback(Models.JsonTag[] tagsToSend)
            {
                // Da die ID in dieser Funktion verwendet wird, 'captured' (erfasst) sie die 
                // lokale Variable 'dbClientId'. Trotzdem ist der Code idiomatisch und sauberer.
                await SendDbUpdateAsync(dbClientId, tagsToSend);
            }

            PlcTagManager.Instance.AddOrUpdateClient(dbClientId, [.. dummyData], SendDbUpdateCallback);

        }

        const int TagsWriteBufferMax = 100;

        private static List<JsonTag> TagsWriteBuffer { get; set; } = [];

        /// <summary>
        /// Schreibt die geänderten Tags in die Datenbank
        /// Nutzt ArrayPool und eine Puffervergrößerungs-Schleife für optimale Performance.
        /// </summary>
        internal static async Task SendDbUpdateAsync(
            Guid clientId,
            JsonTag[] tagsToSend)
        {
            //Sammel-Insert für weniger Schreibvorgänge (Bulk-Insert)
            TagsWriteBuffer.AddRange(tagsToSend);

            if (TagsWriteBuffer.Count < TagsWriteBufferMax) //Wie viele geänderte Tags sollen gepuffert werden (Schreibrate vs. Datenverlustrisiko)
                return;
            
            try
            {               
                Db.InsertTagsBulk([.. TagsWriteBuffer]); // Schreibe die Tags aus dem Buffer in die Datenbank
                TagsWriteBuffer.Clear();
            }
            catch (Exception ex)
            {
                // Fehler beim Senden => Client entfernen (wie im Original-Code)
                DbLogInfo($"Fehler beim Senden an die Datenbank {clientId}. Client wird nicht entfernt.\r\n{ex}");                
            }
        }


        /// <summary>
        /// Liest alle Tag-Namen aus der Tagesdatenbank mit dem Datum <date>. Wenn keine Tags gefunden werden, wird für max. <counter> Tage zurückgegangen, um Tag-Namen zu finden.
        /// </summary>
        /// <param name="date"></param>
        /// <param name="lookBackDays"></param>
        /// <returns>Dictionary <TagName, TagComment></returns>
        public static async Task<List<Tag>> GetDbTagNames(DateTime date, int lookBackDays = 9)
        {
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
                        Db.DbLogInfo($"Tagestabelle: Datei {dbPath} nicht gefunden.");
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
        /// Schreibe die Tags in die Datenbank
        /// </summary>
        /// <param name="jsonTags">Tags zum Schreiben in die Datenbank</param>
        internal static async void InsertTagsBulk(JsonTag[] jsonTags)
        {
            CreateDayDatabaseAsync();

            lock (_dbLock)
            {
                using var connection = new SqliteConnection(DayDbSource);
                connection.Open();
                //await using var transaction = connection.BeginTransaction();

                try
                {

                    // 2. Erstelle den Command mit Parametern
                    var command = connection.CreateCommand();

                    //command.Transaction = transaction; // <- Wichtig: Command muss die Transaktion nutzen!
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

                    // 3. Iteriere und führe den Command für jedes Objekt aus
                    foreach (var tag in jsonTags)
                    {
                        if (tag?.N is null) continue;

                        nameParam.Value = tag.N;
                        valueParam.Value = tag.V;
                        timeParam.Value = tag.T.ToString("yyyy-MM-dd HH:mm:ss");
                        command.ExecuteNonQuery();
                    }

                    // 4. Committe die Transaktion (Hier erfolgt der I/O-Schreibvorgang auf die Platte)
                    //await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    // Bei einem Fehler: Rollback der Transaktion
                    //transaction.Rollback();
                    Db.DbLogInfo($"Fehler beim Batch-Insert. {ex}");
                }
                //finally
                //{
                //    connection.Dispose();
                //}
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

        /// <summary>
        /// Findet TagNames anhand des Kommentars in der Datenbank. (Wenn der TagName schon übergeben wurde, unverändert ausgeben.)
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
        /// <param name="start">Start LocalTime</param>
        /// <param name="end">End LacalTime</param>
        /// <returns>Dictionary <DataBaseName, DataBasePath></returns>
        private static Dictionary<DateTime, string> GetDataBasePathsForQuery(System.DateTime start, System.DateTime end)
        {
            Dictionary<DateTime, string> dataBases = []; //Name, Pfad

            for (DateTime day = start.ToUniversalTime().Date; day.Date <= end.ToUniversalTime().Date; day = day.AddDays(1))
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

        public static async Task<JsonTag[]> GetDataSet(string[] tagNames, System.DateTime start, System.DateTime end)
        {
            List<JsonTag> items = [];

            #region Datenbanken
            //_ = GetAttachedDatabases(true); //Alle angeschlossenen Datenbanken ausdocken (eig. nur nötig wenn vorhergehende Transaktion unvollständig war
            Dictionary<DateTime, string> dataBases = GetDataBasePathsForQuery(start, end);
            #endregion

            lock (_dbLock)
            {
                #region SQLite Verbindung
                using var connection = new SqliteConnection(DayDbSource);
                connection.Open();
                //await using var transaction = connection.BeginTransaction(); Transaction hier nicht gut, weil Datenbanken gelockt werden könnten.
                var command = connection.CreateCommand();

                var nameParam = command.Parameters.Add("@TagName", SqliteType.Text);
                var startParam = command.Parameters.Add("@Start", SqliteType.Text);
                var endParam = command.Parameters.Add("@End", SqliteType.Text);
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
                                query.Add($" SELECT Time, TagValue FROM main.Data WHERE TagId = (SELECT Id FROM main.Tag WHERE Name = @TagName) AND Time BETWEEN @Start AND @End; ");
                            else
                            {
                                attach.Add($"ATTACH DATABASE '{dbChunk[day]}' AS '{dbName}';");
                                query.Add($" SELECT Time, TagValue FROM {dbName}.Data WHERE TagId = (SELECT Id FROM {dbName}.Tag WHERE Name = @TagName) AND Time BETWEEN @Start AND @End ");
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
                        command.CommandText = string.Join(" UNION ", query);
                        //Console.WriteLine(command.CommandText + "\r\n\r\n");

                        foreach (var tagName in tagNames)
                        {
                            if (tagName?.Length < 1)
                                continue; //leerer TagName

                            nameParam.Value = tagName;
                            startParam.Value = start.ToString("yyyy-MM-dd HH:mm:ss");
                            endParam.Value = end.ToString("yyyy-MM-dd HH:mm:ss");

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
                        command.ExecuteNonQueryAsync();


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

        internal static int WriteTag(string tagName, string tagVal, string username)
        {
            try
            {
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(DayDbSource);
                    connection.Open();
                    var command = connection.CreateCommand();

                    command.Parameters.Add("@TagName", SqliteType.Text).Value = tagName;
                    command.Parameters.Add("@TagValue", SqliteType.Blob).Value = tagVal;
                    command.Parameters.Add("@User", SqliteType.Text).Value = username;

                    command.CommandText = @$"
                      INSERT OR IGNORE INTO Tag (Name) VALUES (@TagName); 
                      INSERT INTO Setpoint (TagId, TagValue, User) VALUES (                        
                        (SELECT Id FROM Tag WHERE Name = @TagName)
                        ,@TagValue
                        ,@User
                      );";

                    Console.WriteLine($"WriteTag: Schreibe Tag {tagName} mit Wert {tagVal} von Benutzer {username} in die Datenbank.");

                    return command.ExecuteNonQuery();
                }
            }
            catch 
            {
                throw ;
            }
        }
    }
}
