using Gemini.Models;
using Gemini.Services;
using Microsoft.Data.Sqlite;
using System.Xml.Linq;
using DateTime = System.DateTime;

namespace Gemini.Db
{
    internal partial class Db
    {
        internal static void InitiateDbWriting()
        {
            JsonTag[] dummyData = [];

            //Lade alle Tag - Namen aus der Datenbank
            GetDbTagNames(DateTime.UtcNow).ContinueWith(t =>
            {
                foreach (var kvp in t.Result)
                {
                    dummyData = [.. dummyData, new JsonTag(kvp.Key, null, DateTime.UtcNow)];
                }
            }).Wait();

            var dbClientId = Guid.NewGuid(); //Datenbank wie jeden anderen Client im PlcTagManager anmelden.
            Console.WriteLine($"Die Datenbank loggt sich ein als Clinet {dbClientId}");


            async Task SendDbUpdateCallback(Models.JsonTag[] tagsToSend)
            {
                // Da die ID in dieser Funktion verwendet wird, 'captured' (erfasst) sie die 
                // lokale Variable 'dbClientId'. Trotzdem ist der Code idiomatisch und sauberer.
                await SendDbUpdateAsync(dbClientId, tagsToSend);
            }

            PlcTagManager.Instance.AddOrUpdateClient(dbClientId, dummyData, SendDbUpdateCallback);

        }


        private static List<JsonTag> TagsWriteBuffer { get; set; } = [];

        /// <summary>
        /// Schreibt die geänderten Tags in die Datenbank
        /// Nutzt ArrayPool und eine Puffervergrößerungs-Schleife für optimale Performance.
        /// </summary>
        internal static async Task SendDbUpdateAsync(
            Guid clientId,
            JsonTag[] tagsToSend)
        {
            //Sammel-Insert für weniger Schreibvorgänge. Ideale Umsetzung wäre hier ein Bulk-Insert,
            TagsWriteBuffer.AddRange(tagsToSend);

            if (TagsWriteBuffer.Count < 50)
                return;

            try
            {               
                Db.InsertTagsBulk([.. TagsWriteBuffer]); // Schreibe die Tags aus dem Buffer in die Datenbank
                TagsWriteBuffer.Clear();
            }
            catch (Exception ex)
            {
                // Fehler beim Senden => Client entfernen (wie im Original-Code)
                Console.WriteLine($"Error in sending to Database {clientId}. Client wird nicht entfernt.\r\n{ex}");
                //PlcTagManager.Instance.RemoveClient(clientId);
            }
        }


        /// <summary>
        /// Liest alle Tag-Namen aus der Tagesdatenbank mit dem Datum <date>. Wenn keine Tags gefunden werden, wird für max. <counter> Tage zurückgegangen, um Tag-Namen zu finden.
        /// </summary>
        /// <param name="date"></param>
        /// <param name="lookBackDays"></param>
        /// <returns></returns>
        public static async Task<Dictionary<string, string>> GetDbTagNames(DateTime date, int lookBackDays = 10)
        {
            Dictionary<string, string> tagNames = [];

            await using var connection = new SqliteConnection(DayDbSource);
            await connection.OpenAsync();
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
                    command.CommandText = @"SELECT Name, Comment FROM Tag WHERE ChartFlag == 1;";
                else
                    command.CommandText = $@"
                        ATTACH DATABASE '{dbPath}' AS old_db; 
                        SELECT Name, Comment FROM Tag WHERE ChartFlag == 1; 
                        DETACH DATABASE old_db; ";

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string tagName = await reader.IsDBNullAsync(0) ? string.Empty : reader.GetString(0);
                    string tagComment = await reader.IsDBNullAsync(1) ? string.Empty : reader.GetString(1);    
                    //Console.WriteLine($"Tagestabelle: Gefundener TagName {tagName} mit Kommentar {tagComment}.");
                    tagNames.Add(tagName, tagComment);
                }

                if (tagNames.Count > 0)
                    break;              
            }
            return tagNames;
        }


        /// <summary>
        /// Schreibe die Tags in die Datenbank
        /// </summary>
        /// <param name="jsonTags">Tags zum Schreiben in die Datenbank</param>
        internal static async void InsertTagsBulk(JsonTag[] jsonTags)
        {
            CreateDayDatabaseAsync();

            await using var connection = new SqliteConnection(DayDbSource);
            await connection.OpenAsync();
            await using var transaction = connection.BeginTransaction();

            try
            {
                var command = connection.CreateCommand();
                command.Transaction = transaction; // <- Wichtig: Command muss die Transaktion nutzen!
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
                    nameParam.Value = tag.N;
                    valueParam.Value = tag.V;
                    timeParam.Value = tag.T.ToString("yyyy-MM-dd HH:mm:ss");
                    await command.ExecuteNonQueryAsync();
                }

                // 4. Committe die Transaktion (Hier erfolgt der I/O-Schreibvorgang auf die Platte)
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                // Bei einem Fehler: Rollback der Transaktion
                transaction.Rollback();
                Db.DbLogInfo($"Fehler beim Batch-Insert. {ex}");
            }
        }


        public static async Task<JsonTag[]> GetDataSet(string[] tagNames, System.DateTime start, System.DateTime end)
        {
            List<JsonTag> items = [];

            await using var connection = new SqliteConnection(DayDbSource);
            await connection.OpenAsync();
            
            try
            {
                #region Query zusammenbauen
                
                List<string> attach = [];
                List<string> query = [];
                List<string> dettach = [];

                var command = connection.CreateCommand();
                var dbNameParam = command.Parameters.Add("@DbName", SqliteType.Text);
                //var dbNamesParam = command.Parameters.Add("@DbNames", SqliteType.Text);
                var dbPathParam = command.Parameters.Add("@DbPath", SqliteType.Text);
                var nameParam = command.Parameters.Add("@TagName", SqliteType.Text);
                var startParam = command.Parameters.Add("@Start", SqliteType.Text);
                var endParam = command.Parameters.Add("@End", SqliteType.Text);
                
                Dictionary<string, string> dataBases = [];

                for (DateTime day = start; day.Date <= end.Date; day = day.AddDays(1))
                {
                    string dbPath = GetDayDbPath(day);
                    if (!File.Exists(dbPath))
                    {
#if DEBUG
                        Console.WriteLine($"Datenbank {dbPath} für Tag {day:yyyy-MM-dd} existiert nicht.");
#endif
                        continue;
                    }

                    string dbName = $"db{day.Year:00}{day.Month:00}{day.Day:00}";
                    dataBases[dbName] = dbPath;

                    Console.WriteLine($"DB-Name {dbName}");

                    if (day.Date == DateTime.UtcNow.Date)
                        query.Add($" SELECT Time, TagValue FROM main.Data WHERE TagId = (SELECT Id FROM main.Tag WHERE Name = @TagName) AND Time BETWEEN @Start AND @End; ");
                    else
                    {                        
                        query.Add($" SELECT Time, TagValue FROM {dbName}.Data WHERE TagId = (SELECT Id FROM {dbName}.Tag WHERE Name = @TagName) AND Time BETWEEN @Start AND @End ");
                        dettach.Add($"DETACH DATABASE '{dbName}';");
                    }
                    
                    dbPathParam.Value = dbPath;
                    dbNameParam.Value = dbName;
                }


                #endregion

                #region Datenbanken anhängen

                #region Bereits angehängte Datenbanken finden

                command.CommandText = "SELECT name FROM pragma_database_list;";               
                List<string> existingDbs = [];
                await using var reader1 = await command.ExecuteReaderAsync();
                while (await reader1.ReadAsync())
                {
                    existingDbs.Add(reader1.GetString(0));
                }

                await reader1.CloseAsync();

                dataBases = dataBases
                    .Where(kvp => !existingDbs.Contains(kvp.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                #endregion

                foreach (var db in dataBases) 
                    attach.Add($"ATTACH DATABASE '{db.Value}' AS {db.Key};");                    
                

                string controoling = string.Empty;
                command.CommandText = string.Join(' ', attach);
                controoling += command.CommandText;
                Console.WriteLine(command.CommandText);
                command.ExecuteNonQuery();

                #endregion

                #region Abfrage ausführen

                command.CommandText = string.Join(" UNION ", query);
                controoling += command.CommandText;
                //Console.WriteLine("Länge: " + query.Count() + "\r\n" + command.CommandText);

                DateTime minTime = DateTime.Now;

                foreach (var tagName in tagNames)
                {
                    nameParam.Value = tagName;
                    startParam.Value = start.ToString("yyyy-MM-dd HH:mm:ss");
                    endParam.Value = end.ToString("yyyy-MM-dd HH:mm:ss");

                    Console.WriteLine($"GetDataSet() Abfrage '{nameParam.Value}' von '{startParam.Value}' bis '{endParam.Value}'");
                  
                    await using var reader2 = await command.ExecuteReaderAsync();                    
                    while (await reader2.ReadAsync())
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

                        if (t < minTime)
                            minTime = t;

                        items.Add(new JsonTag(tagName, value, t));
                    }
                }

                Console.WriteLine($"Frühester Datenpunkt war {minTime}");
                #endregion

                #region Datenbanken wieder lösen (notwendig?)

                command.CommandText = string.Join(' ', dettach);
                controoling += command.CommandText;
                //Console.WriteLine(command.CommandText);
                command.ExecuteNonQuery();

                #endregion

                Console.WriteLine("Vollständiger Commend:\r\n" + controoling + "\r\n");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetDataSet() Fehler beim Auslesen des Datensatzes für Tags {string.Join(' ', tagNames)}:\r\n{ex}");
            }

            //Console.WriteLine($"GetDataSet() {items.Count} Ergebnisse: " + string.Join(' ', items.Select(n => n.V)));

            return [.. items];
        }


        /// <summary>
        /// Findet TagNames anhand des Kommentars in der Datenbank. (Wenn der TagName schon übergeben wurde, unverändert ausgeben.)
        /// </summary>
        /// <param name="comments">Liste von Tag-Kommentarten</param>
        /// <returns>Liste von TagNames</returns>
        internal static async Task<Dictionary<string, string>> GetTagNamesFromComments(string[] comments)
        {
            Dictionary<string, string> tags = [];

            await using var connection = new SqliteConnection(DayDbSource);
            await connection.OpenAsync();
            var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT Name FROM TAG WHERE Comment = @Comment OR Name = @Comment;
            ";

            var commentParam = command.Parameters.Add("@Comment", SqliteType.Text);

            foreach (var comment in comments)
            {
                commentParam.Value = comment;

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string tagName = reader.GetString(0);
                    tags.Add(tagName, comment);
                }
            }

            return tags;
        }
    }
}
