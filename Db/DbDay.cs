using Gemini.Models;
using Gemini.Services;
using Microsoft.Data.Sqlite;
using System.Buffers;
using System.Data;
using System.Linq;
using System.Net.WebSockets;
using System.Text.Json;
using static Gemini.Middleware.WebSocketMiddleware;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Gemini.Db
{
    internal partial class Db
    {
        internal static void InitiateDbWriting()
        {
            JsonTag[] dummyData = [
                new JsonTag("A01_DB10_DBW2", null, DateTime.UtcNow),
                new JsonTag("A01_DB10_DBW4", null, DateTime.UtcNow),
                new JsonTag("A01_DB10_DBW6", null, DateTime.UtcNow)
            ];

            var dbClientId = Guid.NewGuid(); //Datenbank wie jeden anderen Client im PlcTagManager anmelden.

            //// Callback meldet Updates an den Client                    
            //Func<Models.JsonTag[], Task> sendCallback = (tagsToSend) =>
            //    SendDbUpdateAsync(dbClientId, tagsToSend);

            //// Registriere Client und seine Tags beim globalen Manager
            //PlcTagManager.Instance.AddOrUpdateClient(dbClientId, dummyData, sendCallback);



            async Task SendDbUpdateCallback(Models.JsonTag[] tagsToSend)
            {
                // Da die ID in dieser Funktion verwendet wird, 'captured' (erfasst) sie die 
                // lokale Variable 'dbClientId'. Trotzdem ist der Code idiomatisch und sauberer.
                await SendDbUpdateAsync(dbClientId, tagsToSend);
            }

            PlcTagManager.Instance.AddOrUpdateClient(dbClientId, dummyData, SendDbUpdateCallback);


        }


        /// <summary>
        /// Schreibt die geänderten Tags in die Datenbank
        /// Nutzt ArrayPool und eine Puffervergrößerungs-Schleife für optimale Performance.
        /// </summary>
        internal static async Task SendDbUpdateAsync(
            Guid clientId,
            JsonTag[] tagsToSend)
        {

            try
            {
                //TODO: Sammel-Insert für bessere Performance implementieren

                Db.InsertTags(tagsToSend); // Schreibe die Tags in die Datenbank

            }
            catch (Exception ex)
            {
                // Fehler beim Senden => Client entfernen (wie im Original-Code)
                Console.WriteLine($"Error in sending to Database {clientId}. Client wird nicht entfernt.\r\n{ex}");
                //PlcTagManager.Instance.RemoveClient(clientId);
            }
        }



        /// <summary>
        /// Schreibe die Tags in die Datenbank
        /// </summary>
        /// <param name="jsonTags">Tags zum Schreiben in die Datenbank</param>
        internal static async void InsertTags(JsonTag[] jsonTags)
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
                      INSERT INTO Data (TagId, TagValue) VALUES (
                        (SELECT Id FROM Tag WHERE Name = @TagName)
                        ,@TagValue
                      );";

                var nameParam = command.Parameters.Add("@TagName", SqliteType.Text);
                var valueParam = command.Parameters.Add("@TagValue", SqliteType.Blob);

                // 3. Iteriere und führe den Command für jedes Objekt aus
                foreach (var tag in jsonTags)
                {
                    nameParam.Value = tag.N;
                    valueParam.Value = tag.V;
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


        public static async Task<JsonTag[]> GetDataSet(string[] tagNames, DateTime start, DateTime end)
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
                var dbPathParam = command.Parameters.Add("@DbPath", SqliteType.Text);
                var nameParam = command.Parameters.Add("@TagName", SqliteType.Text);
                var startParam = command.Parameters.Add("@Start", SqliteType.Text);
                var endParam = command.Parameters.Add("@End", SqliteType.Text);

                for (DateTime day = start; day <= end; day = day.AddDays(1))
                {
                    string dbPath = GetDayDbPath(day);
                    string dbName = $"db{day.Year:00}{day.Month:00}{day.Day:00}";
                    //Console.WriteLine($"DB {dbPath} heißt {dbName}.");

                    if (day.Date == DateTime.Now.Date)
                        query.Add($" SELECT Time, TagValue FROM main.Data WHERE TagId = (SELECT Id FROM main.Tag WHERE Name = @TagName) AND Time BETWEEN @Start AND @End ");
                    else
                    {
                        attach.Add($" ATTACH DATABASE @DbPath AS {dbName};");
                        query.Add($" SELECT Time, TagValue FROM {dbName}.Data WHERE TagId = (SELECT Id FROM {dbName}.Tag WHERE Name = @TagName) AND Time BETWEEN @Start AND @End ");
                        dettach.Add($" DETACH DATABASE {dbName};");
                    }

                    dbPathParam.Value = dbPath;
                    dbNameParam.Value = dbName;
                }

                #endregion

                #region Datenbanken anhängen

                command.CommandText = string.Join(' ', attach);
                //Console.WriteLine(command.CommandText);
                command.ExecuteNonQuery();

                #endregion

                #region Abfrage ausführen

                command.CommandText = string.Join(" UNION ", query);
                //Console.WriteLine(command.CommandText);

                foreach (var tagName in tagNames)
                {
                    nameParam.Value = tagName;
                    startParam.Value = start.ToString("o");
                    endParam.Value = end.ToString("o");

                    //Console.WriteLine($"GetDataSet() {dbName} Abfrage '{nameParam.Value}' von '{startParam.Value}' bis '{endParam.Value}'");

                    await using var reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        string v = reader.GetString(1);
                        //Console.WriteLine($"Gelesener Wert für Tag {tagName}: {v}");
                        object? value = null;

                        if (double.TryParse(v, out double floatValue))
                            value = floatValue;
                        else if (Int16.TryParse(v, out Int16 intValue))
                            value = intValue;
                        else if (bool.TryParse(v, out bool boolValue))
                            value = boolValue;

                        items.Add(new JsonTag(tagName, value, reader.GetDateTime(0).ToLocalTime()));
                    }
                }

                #endregion

                #region Datenbanken wieder lösen (notwendig?)

                command.CommandText = string.Join(' ', dettach);
                Console.WriteLine(command.CommandText);
                command.ExecuteNonQuery();

                #endregion

            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetDataSet() Fehler beim Auslesen des Datensatzes für Tags {string.Join(' ', tagNames)}:\r\n{ex}");
            }

            return [.. items];
        }

    }
}
