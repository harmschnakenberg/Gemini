using Gemini.Models;
using Microsoft.Data.Sqlite;
using SQLitePCL;
using System.Data;
using System.Data.Common;

namespace Gemini.Db
{
   
    internal partial class Db
    {

        private static readonly Lock _dbLock = new();

        #region Pfade
        internal static readonly string AppFolder = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string masterDbPath = Path.Combine(AppFolder, "db", "Master.db");
        private static readonly string MasterDbSource = "Data Source=" + masterDbPath;
        private static readonly string DayDbSource = "Data Source=" + GetDayDbPath(DateTime.UtcNow);

        public Db()
        {
            //Stelle sicher, dass die Datenbankordner existieren
            string dbFolder = Path.Combine(AppFolder, "db");
            if (!Directory.Exists(dbFolder))
            {
                Directory.CreateDirectory(dbFolder);
            }
            //Master-Datenbank erstellen, falls nicht vorhanden
            if (!File.Exists(masterDbPath))
            {
                CreateMasterDatabaseAsync();
            }
            //Tages-Datenbank erstellen, falls nicht vorhanden
            string dayDbPath = GetDayDbPath(DateTime.UtcNow);
            if (!File.Exists(dayDbPath))
            {
                CreateDayDatabaseAsync();
            }
        }

        static string GetDayDbPath(DateTime date)
        {
            return Path.Combine(AppFolder, "db", date.ToUniversalTime().ToString("yyyyMMdd") + ".db");
        }

        #endregion

        // Ensure the native SQLite library is loaded
        // Batteries_V2.Init();

        private static async void CreateMasterDatabaseAsync()
        {
            if (File.Exists(masterDbPath))
                return;

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
                    PRAGMA journal_mode=WAL;
                    ";
            int result = command.ExecuteNonQuery();
            Console.WriteLine("Mastertabelle erstellt. Ergebnis: " + result);

            if (result != 0) //Keine Änderungen geschrieben
                return;

            //Tabellen mit Default-Daten füllen
            command.CommandText =
            $@"
                    INSERT INTO Log (Category, Message) VALUES ('System', 'Datenbank neu erstellt.'); 
                    INSERT INTO User (Name, IsAdmin, Password) VALUES ('admin', 1, '{Encrypt("admin")}'); 
                    INSERT INTO Source (Name, Ip) VALUES ('A01', '192.168.0.10'); ";
            _ = command.ExecuteNonQuery();
        }

        private static async void CreateDayDatabaseAsync()
        {
            if (File.Exists(GetDayDbPath(DateTime.UtcNow)))
                return;

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

                          CONSTRAINT fk_TagId FOREIGN KEY (TagId) REFERENCES Tag (Id) ON DELETE NO ACTION
                          ); 
                          PRAGMA journal_mode=WAL;
                    ";
            int result = command.ExecuteNonQuery();

            Console.WriteLine("Tagestabelle erstellt. Ergebnis: " + result);

            if (result != 0) //Keine Änderungen geschrieben. Warum 0?
                return;

            #region Tabelle Tag mit TagNames aus der letzten Tabelle füllen

            
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
                    Db.DbLogInfo($"Tagestabelle: Datei {dbPath} nicht gefunden.");
#endif
                    continue;
                }

                //Console.WriteLine($"Tagestabelle: Datei {dbPath} gefunden.");

                command.CommandText = $@"
                        ATTACH DATABASE '{dbPath}' AS old_db; 
                        INSERT INTO Tag SELECT * FROM old_db.Tag; 
                        DETACH DATABASE old_db; 
                        ";

                result = command.ExecuteNonQuery();

                if (result > 0) //Es wurden Tags übernommen
                {
                    Db.DbLogInfo($"Tagestabelle erstellt; Übernehme {result} Tags aus {dbPath}");
                    return;
                }
            }

            Console.WriteLine("Tagestabelle erstellt; Keine Tags übernommen.");
            #endregion

        }


    }
}
