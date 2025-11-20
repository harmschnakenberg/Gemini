using Gemini.Models;
using Microsoft.Data.Sqlite;
using SQLitePCL;
using System.Data;
using System.Data.Common;

namespace Gemini.Db
{


    internal partial class Db
    {
        #region Pfade
        private static readonly string AppFolder = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string MasterDbSource = "Data Source=" + Path.Combine(AppFolder, "db", "Master.db");
        private static readonly string DayDbSource = "Data Source=" + GetDayDbPath(DateTime.UtcNow);

        static string GetDayDbPath(DateTime date)
        {
            return Path.Combine(AppFolder, "db", date.ToUniversalTime().ToString("yyyyMMdd") + ".db");
        }

        #endregion

        // Ensure the native SQLite library is loaded
        // Batteries_V2.Init();

        private static async void CreateMasterDatabaseAsync()
        {
            await using var connection = new SqliteConnection(MasterDbSource);
            connection.Open();
            using var command = connection.CreateCommand();
            //Tabellen erstellen
            command.CommandText =
            @"
                    CREATE TABLE IF NOT EXISTS Log ( 
                          Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, 
                          Time TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, 
                          Category TEXT NOT NULL,
                          Message TEXT 
                          ); 
                    CREATE TABLE IF NOT EXISTS User ( 
                          Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                          Name TEXT NOT NULL UNIQUE,
                          IsAdmin INTEGER DEFAULT 0,
                          Password TEXT,
                          Identification TEXT
                          ); 
                    CREATE TABLE IF NOT EXISTS Source ( 
                          Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                          Name TEXT NOT NULL UNIQUE,     
                          CpuType STRING NOT NULL DEFAULT 'S71500',  
                          Ip TEXT,                                                     
                          Rack INTEGER DEFAULT 0,
                          Slot INTEGER DEFAULT 0,
                          Comment TEXT                          
                          );
                    ";
            int result = command.ExecuteNonQuery();

            if (result < 1) //Keine Änderungen geschrieben
                return;

            //Tabellen mit Default-Daten füllen
            command.CommandText =
            $@"
                    INSERT INTO Log (Category, Content) VALUES ('System', 'Datenbank neu erstellt.'); 
                    INSERT INTO User (Name, IsAdmin, Password) VALUES ('admin', 1, '{Encrypt("admin")}'); 
                    INSERT INTO Source (Name, Ip) VALUES ('A01', '192.168.0.10'); ";
            _ = command.ExecuteNonQuery();
        }

        private static async void CreateDayDatabaseAsync()
        {
            await using var connection = new SqliteConnection(DayDbSource);
            connection.Open();
            using var command = connection.CreateCommand();
            //Tabellen erstellen
            command.CommandText =
            @"
                    CREATE TABLE IF NOT EXISTS Tag (       
                          Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                          Name TEXT NOT NULL UNIQUE,                          
                          Comment TEXT,
                          ChartFlag INTEGER DEFAULT 0,
                          LogFlag INTEGER DEFAULT 0
                          ); 
                          CREATE TABLE IF NOT EXISTS Data (                         
                          Time TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, 
                          TagId INT NOT NULL,
                          TagValue NUMERIC,

                          CONSTRAINT fk_TagId FOREIGN KEY (TagId) REFERENCES TagNames (Id) ON DELETE NO ACTION
                          ); 
                    ";
            int result = command.ExecuteNonQuery();

            if (result < 1) //Keine Änderungen geschrieben
                return;

            #region Tabelle Tag mit TagNames aus der letzten Tabelle füllen

            DataTable dt = new();
            DateTime date = DateTime.UtcNow;
            int counter = 10; // limitieren, wie weit in die Vergangenheit geschaut werden soll

            //Wenn die Datenbank grade neu erstellt wurde ist sie leer.
            //Deshalb die letzte Tages-DB suchen und TagNames übernehmen
            //Wenn die letzte Tages-DB keine TagNames hat, wird eine weitere gesucht, bis counter = 0 ist
            while (result <= 0 && --counter > 0)
            {
                date = date.AddDays(-1);
                string dbPath = GetDayDbPath(date);

                if (!File.Exists(dbPath))
                {
#if DEBUG
                    Console.WriteLine($"Tagestabelle: Datei {dbPath} nicht gefunden.");
#endif
                    continue;
                }

                command.CommandText = $@"
                        ATTACH DATABASE '{dbPath}' AS old_db; 
                        INSERT INTO Tag SELECT * FROM old_db.Tag; 
                        DETACH DATABASE old_db; ";

                result = command.ExecuteNonQuery();

                if (result > 0) //Es wurden Tags übernommen                         
                    Db.DbLogInfo($"Tagestabelle erstellt; Übernehme {result} Tags aus {dbPath}");
            }

            #endregion

        }




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
                // Logging des Fehlers (in einem echten Fall)
                Db.DbLogInfo($"Fehler beim Batch-Insert. {ex}" );                
            }
        }
    }
}
