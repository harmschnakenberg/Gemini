using Gemini.DynContent;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using System.Data.Common;
using System.Runtime.CompilerServices;

namespace Gemini.Db
{

    internal sealed partial class Db
    {

        private static readonly Lock _dbLock = new();

        #region Pfade
        /* Warum Ihre ursprüngliche Variante nicht funktioniert hat:
            private static string DayDbSource { get; } = "Data Source=" + DayDbPath; 
           ist ein Auto-Property Initializer. Der Wert wird einmalig berechnet und im Hintergrund in einem statischen Feld gespeichert.           
         */

        internal static readonly string AppFolder = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string MasterDbPath = Path.Combine(AppFolder, "db", "Master.db");
        private static readonly string MasterDbSource = "Data Source=" + MasterDbPath;
        private static string DayDbPath => GetDayDbPath(DateTime.UtcNow);
        // Jedes Mal, wenn DayDbSource abgerufen wird, wird es neu berechent. Das ist notwendig, damit immer die aktuelle Tagesdatenbank angesprochen wird.
        private static string DayDbSource =>  "Data Source=" + DayDbPath;

        static Db()
        {           
            //Stelle sicher, dass die Datenbankordner existieren
            string dbFolder = Path.Combine(AppFolder, "db");

            if (!Directory.Exists(dbFolder))
            {
                Directory.CreateDirectory(dbFolder);
            }
            //Master-Datenbank erstellen, falls nicht vorhanden
            if (!File.Exists(MasterDbPath))
            {
                CreateMasterDatabaseAsync();
            }
            //Tages-Datenbank erstellen, falls nicht vorhanden
            if (!File.Exists(DayDbPath))
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

        private static void CreateMasterDatabaseAsync()
        {
            if (File.Exists(MasterDbPath))
                return;

            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
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
                    CREATE TABLE IF NOT EXISTS Roles ( 
                          Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                          Role TEXT NOT NULL UNIQUE
                          ); 
                    CREATE TABLE IF NOT EXISTS User ( 
                          Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                          Name TEXT NOT NULL UNIQUE,                         
                          Hash TEXT,
                          RoleId INTEGER,
                         
                          CONSTRAINT fk_RoleId FOREIGN KEY (RoleId) REFERENCES Roles (Id) ON DELETE NO ACTION
                          );                     
                    CREATE TABLE IF NOT EXISTS Source ( 
                          Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                          Name TEXT NOT NULL UNIQUE,     
                          CpuType STRING NOT NULL DEFAULT 'S71500',  
                          Ip TEXT,                                                     
                          Rack INTEGER DEFAULT 0,
                          Slot INTEGER DEFAULT 0,
                          IsActive INTEGER DEFAULT 1,
                          Comment TEXT                          
                          );
                    CREATE TABLE IF NOT EXISTS ReadFailure ( 
                          Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                          Time TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,                               
                          Ip STRING NOT NULL,  
                          Db INTEGER,
                          StartByte INTEGER,
                          Length INTEGER                      
                          );
                    CREATE TABLE IF NOT EXISTS ChartConfig ( 
                          Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                          Name TEXT NOT NULL,                               
                          Author TEXT NOT NULL,  
                          Start TEXT,
                          End TEXT,
                          Interval TEXT,
                          Tags TEXT
                          );

                    PRAGMA journal_mode=WAL;
                    PRGAMA foreign_keys = ON;
                    PRAGMA synchronous = NORMAL;
                    ";
                int result = command.ExecuteNonQuery();

                Db.DbLogInfo("Mastertabelle erstellt. Ergebnis: " + result);

                if (result != 0) //Keine Änderungen geschrieben
                    return;

                //Tabellen mit Default-Daten füllen
                command.CommandText =
                $@"
                    INSERT INTO Log (Category, Message) VALUES ('System', 'Datenbank neu erstellt.');                      
                    INSERT INTO Source (Name, Ip) VALUES ('A01', '192.168.0.10'); 
                    INSERT INTO Roles (Id, Role) VALUES ({(int)Role.Unbekannt}, '{Role.Unbekannt}'); 
                    INSERT INTO Roles (Id, Role) VALUES ({(int)Role.Admin},'{Role.Admin}'); 
                    INSERT INTO Roles (Id, Role) VALUES ({(int)Role.User},'{Role.User}'); 
                    INSERT INTO Roles (Id, Role) VALUES ({(int)Role.Guest},'{Role.Guest}'); 
                    INSERT INTO ChartConfig (Name, Author, Start, End, Interval, Tags) VALUES ('Test', 'Admin', '{DateTime.Now.AddDays(-1):yyyy-MM-dd HH:mm:ss}','{DateTime.Now.AddDays(1):yyyy-MM-dd HH:mm:ss}', '{MiniExcel.Interval.Minute}', '{{""dataType"":""Map"",""value"":[[""A01_DB10_DBW2"",""Stunden""],[""A01_DB10_DBW4"",""Minuten""],[""A01_DB10_DBW6"",""Sekunden""]]}}');
            ";


                _ = command.ExecuteNonQuery();
            }
            _ = Db.CreateUser("Admin", "Admin", Role.Admin);
        }

        private static void CreateDayDatabaseAsync()
        {                
            if (File.Exists(DayDbPath))
                return;
            try
            {
                lock (_dbLock)
                {                    
                    using var connection = new SqliteConnection(DayDbSource);
                    connection.Open();
                    using var command = connection.CreateCommand();
                    //Tabellen erstellen
                    command.CommandText =
                    @"  CREATE TABLE IF NOT EXISTS Tag (       
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
                          
                    CONSTRAINT uk_Entry UNIQUE (TagId, Time) ON CONFLICT IGNORE,
                    CONSTRAINT fk_TagId FOREIGN KEY (TagId) REFERENCES Tag (Id) ON DELETE NO ACTION
                    ); 
                
                    CREATE TABLE IF NOT EXISTS Setpoint (                         
                    Time TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, 
                    TagId INT NOT NULL,
                    TagValue NUMERIC,
                    OldValue NUMERIC,
                    User TEXT,

                    CONSTRAINT fk_TagId FOREIGN KEY (TagId) REFERENCES Tag (Id) ON DELETE NO ACTION
                    );

                    CREATE VIEW DataMinute AS
                    SELECT Time, TagId, MIN(TagValue) AS TagValue
                    FROM Data			
                    GROUP BY TagId, strftime ('%Y%m%d %H:%M', Time)	
                    UNION
                    SELECT Time, TagId, MAX(TagValue) 
                    FROM Data		
                    GROUP BY TagId, strftime ('%Y%m%d %H:%M', Time)
                    ORDER BY Time;

                    PRAGMA journal_mode=WAL;
                    ";

                    /* Nur den letzten Wert aus jeder Minute. Ergibt ein verzerrtes Bild der Daten.
                    CREATE VIEW IF NOT EXISTS DataMinute AS
                    SELECT * FROM Data
                    GROUP BY TagId, strftime ('%Y%m%d %H:%M', Time)
                    ORDER BY Time;
                    */

                    int result = command.ExecuteNonQuery();
#if DEBUG
                    Db.DbLogInfo("Tagestabelle erstellt. Ergebnis: " + result);
#endif
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
                            Db.DbLogInfo($"Tagestabelle: Datei {Path.GetFileName(dbPath)} nicht gefunden.");
#endif
                            continue;
                        }

                        //Console.WriteLine($"Tagestabelle: Datei {dbPath} gefunden.");

                        command.CommandText =
                                $"ATTACH DATABASE '{dbPath}' AS old_db; " +
                                // "VACUUM old_db; " +
                                "INSERT OR IGNORE INTO Tag SELECT NULL, Name, Comment, ChartFlag, LogFlag FROM old_db.Tag " + //Id nicht übernehmen, weil die Id sonst immer weiter gezählt werden. IGNORE darf eigentlich nicht notwendig sein, ist es aber scheinbar?.
                                "WHERE old_db.Tag.ChartFlag > 0 OR length( old_db.Tag.Comment ) > 0; " + //nur TagNames, die aufgezeichnet werden oder Kommentare erhalten haben. (Alle anderen werden beim ersten Lesen erneut in die Tabelle geschrieben).
                                "DETACH DATABASE old_db; ";

                        result = command.ExecuteNonQuery();

                        if (result > 0) //Es wurden Tags übernommen
                        {
                            Db.DbLogInfo($"Tagestabelle {Path.GetFileName(DayDbPath)} erstellt; Übernehme {result} Tags aus {Path.GetFileName(dbPath)}");
                            return;
                        }
                    }
                }
                Db.DbLogWarn($"Tagestabelle {Path.GetFileName(DayDbPath)} ({DayDbSource}) erstellt; Keine Tags übernommen.");
                #endregion


            }
            catch (Exception ex)
            {
                Db.DbLogError("Fehler beim Erstellen der Tagesdatenbank: " + ex.Message);
            }
        }

        internal static long GetAllDbSizesInMBytes(out int dbFileCount)
        {
            long totalSize = 0;
            dbFileCount = 0;
            string dbFolder = Path.Combine(AppFolder, "db");

            if (Directory.Exists(dbFolder))
            {
                var dbFiles = Directory.GetFiles(dbFolder, "*.db", SearchOption.TopDirectoryOnly);
                foreach (var file in dbFiles)
                {
                    FileInfo fileInfo = new(file);
                    totalSize += fileInfo.Length;
                }
                dbFileCount = dbFiles.Length;
            }
            //Größe in MB zurückgeben
            return totalSize / (1024 * 1024); ;
        }

        /// <summary>
        /// Performs a checkpoint and vacuum operation on all SQLite database files in the application's database folder
        /// to reclaim disk space and optimize database files.
        /// </summary>
        /// <remarks>This method processes all databases with write-ahead log (WAL) files in the
        /// application's database directory. It is intended for internal maintenance and should be called when database
        /// cleanup is required. The operation may temporarily lock each database while maintenance is
        /// performed.</remarks>
        internal static void VaccumAllDatabases()
        {
            var dbFolder = Path.Combine(AppFolder, "db");
            var walFiles = Directory.EnumerateFiles(dbFolder, "*.db-wal", SearchOption.TopDirectoryOnly);

            foreach (var walFile in walFiles)
            {
                lock (_dbLock)
                {
                    var dbPath = Path.ChangeExtension(walFile, ".db");
                    if (!File.Exists(dbPath)) continue;
                    //Console.WriteLine($"{dbPath} aufräumen..");
                    Db.DbLogInfo($"Datenbank {Path.GetFileName(dbPath)} aufräumen..");
                    using var connection = new SqliteConnection("Data Source=" + dbPath);
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText =
                        "PRAGMA wal_checkpoint(TRUNCATE);" +
                        "VACUUM;";
                    command.ExecuteNonQuery();
                }
            }

        }
    }
}
