using Gemini.DynContent;
using Gemini.Models;
using Microsoft.Data.Sqlite;
using S7.Net;

namespace Gemini.Db
{
    public enum Role
    {
        Unbekannt,
        Admin,        
        User,
        Guest
    }

    internal class User 
    {
        public required string Name { get; set; }
        public required Role Role { get; set; }
    }

    internal class PlcConf
    {
        internal PlcConf(int id, string name, CpuType cpuType, string ip, short rack, short slot, bool isActive, string comment)
        {
            Id = id;
            Name = name;
            CpuType = cpuType;
            Ip = ip;
            Rack = rack;
            Slot = slot;
            IsActive = isActive;
            Comment = comment;
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public CpuType CpuType { get; set; }
        public string Ip { get; set; }
        public short Rack { get; set; }
        public  short Slot { get; set; }
        public bool IsActive { get; set; }        
        public string Comment { get; set; }

        internal Plc GetPlc()
        {
            return new Plc(this.CpuType, this.Ip, this.Rack, this.Slot);
        }

    }


    internal partial class Db
    {
        #region Benutzerverwaltung
        internal static bool AuthenticateUser(string username, string userpassword, out Role userRole)
        {
            userRole = Role.Unbekannt;

            lock (_dbLock)
            {

                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();

                var command = connection.CreateCommand();
                var nameParam = command.Parameters.Add("@Name", SqliteType.Text);
                var pswdParam = command.Parameters.Add("@Password", SqliteType.Text);

                var query = @"SELECT Hash, RoleId FROM User WHERE Name = @Name;";
                command.CommandText = query;
                nameParam.Value = username;

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    userRole = (Role)reader.GetInt32(1);
                    string storedHash = reader.GetString(0);
                    //Console.WriteLine($"Vergleich {userpassword}\t{storedHash}  {Gemini.Middleware.PasswordHasher.HashPassword(userpassword)}");
                    bool passwordMatch = Gemini.Middleware.PasswordHasher.VerifyPassword(userpassword, storedHash);                    
                    return passwordMatch;
                }

                connection.Dispose();
            }
            return false;
        }


        internal static List<User> SelectAllUsers()
        {
            List<User> users = [];

            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();
                var query = @"SELECT Name, RoleId FROM User;";
                command.CommandText = query;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string userName = reader.GetString(0);
                    Role userRole = (Role)reader.GetInt32(1);                    
                    users.Add(new User(){ Name = userName, Role = userRole});
                }
                connection.Dispose();
            }
            return users;
        }

        //internal static string GetUserRole(string username)
        //{
        //    lock (_dbLock)
        //    {
        //        using var connection = new SqliteConnection(MasterDbSource);
        //        connection.Open();
        //        var command = connection.CreateCommand();
        //        var nameParam = command.Parameters.Add("@Name", SqliteType.Text);
        //        var query = @"SELECT Role FROM User WHERE Name = @Name;";
        //        command.CommandText = query;
        //        nameParam.Value = username;
        //        using var reader = command.ExecuteReader();
        //        while (reader.Read())
        //        {
        //            string userRole = reader.GetString(0);
        //            return userRole;
        //        }
        //        connection.Dispose();
        //    }
        //    return "Unknown";
        //}

        internal static int CreateUser(string name, string password, Role role = Role.Guest)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText =
                @"
                    INSERT INTO User (Name, Hash, RoleId) 
                    VALUES (@Name, @Hash, @RoleId); ";
                command.Parameters.AddWithValue("@Name", name);
                string passwordHash = Gemini.Middleware.PasswordHasher.HashPassword(password);
                command.Parameters.AddWithValue("@Hash", passwordHash);
                command.Parameters.AddWithValue("@RoleId", (int)role);
                return command.ExecuteNonQuery();
            }
        }

        internal static int UpdateUser(string name, string password, Role role = Role.Guest)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();

                command.Parameters.AddWithValue("@Name", name);
                command.Parameters.AddWithValue("@RoleId", (int)role);

                if (password.IsWhiteSpace())
                {
                    //Console.WriteLine("Benutzer geändert ohne Passwortänderung.");
                    command.CommandText =
                @"  UPDATE User 
                    SET RoleId = @RoleId
                    WHERE Name = @Name; ";
                }
                else
                {
                    //Console.WriteLine("Benutzer geändert mit Passwortänderung.");
                    command.CommandText =
                @"  UPDATE User 
                    SET Hash = @Hash, RoleId = @RoleId
                    WHERE Name = @Name; ";

                    string passwordHash = Gemini.Middleware.PasswordHasher.HashPassword(password);
                    command.Parameters.AddWithValue("@Hash", passwordHash);
                }

                return command.ExecuteNonQuery();
            }
        }

        internal static int DeleteUser(string name)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText =
                $@" DELETE FROM User 
                    WHERE Name = @Name AND (
                      RoleId != @AdminRoleId OR
                      (SELECT COUNT(RoleId) FROM User WHERE RoleId = @AdminRoleId) > 1
                    ); "; // ein Admin muss immer übrig bleiben
                command.Parameters.AddWithValue("@Name", name);
                command.Parameters.AddWithValue("@AdminRoleId", (int)Role.Admin);
                return command.ExecuteNonQuery();
            }
        }


        #endregion

        #region Logging

        private static readonly ILogger<Db>? _logger;

        internal static void DbLogPurge(int limit = 100000)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();

                command.Parameters.AddWithValue("@Limit", limit);
                command.CommandText =
                @" DELETE FROM Log WHERE Id NOT IN (SELECT Id FROM Log ORDER BY Id DESC LIMIT @Limit); ";

                _ = command.ExecuteNonQuery();
            }

        }

        private static async void DbLog(string category, string message)
        {
            //CreateMasterDatabaseAsync();

            try
            {
                await using var connection = new SqliteConnection(MasterDbSource);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText =
                    @"INSERT INTO Log (Category, Message) VALUES (@Category, @Message); ";

                command.Parameters.AddWithValue("@Category", category);
                command.Parameters.AddWithValue("@Message", message);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError("Fehler beim Schreiben des Logs in die Datenbank: {ErrorMessage}", ex.Message);
            }
        }

        internal static async void DbLogInfo(string message)
        {            
            DbLog("Info", message);
            if (_logger?.IsEnabled(LogLevel.Information) == true)            
            {
                // CA1873: Vermeide teure Auswertung, wenn Logging deaktiviert ist
                _logger.LogInformation("{Message}", message);
            }
        }

        internal static async void DbLogWarn(string message)
        {
            DbLog("Warn", message);

            if (_logger?.IsEnabled(LogLevel.Warning) == true)            
            {
                // CA1873: Vermeide teure Auswertung, wenn Logging deaktiviert ist
                _logger.LogWarning("{Message}", message);            
            }
        }

        internal static async void DbLogError(string message)
        {
            DbLog("Error", message);

            if (_logger?.IsEnabled(LogLevel.Error) == true)            
            {
                // CA1873: Vermeide teure Auswertung, wenn Logging deaktiviert ist
                _logger.LogError("{Message}", message);            
            }
        }

        internal static void DbLogReadFailure(string plcIp, int db, int startByte, int length)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();

                command.Parameters.AddWithValue("@Ip", plcIp);
                command.Parameters.AddWithValue("@Db", db);
                command.Parameters.AddWithValue("@StartByte", startByte);
                command.Parameters.AddWithValue("@Length", length);

                command.CommandText = @" UPDATE ReadFailure SET Time = CURRENT_TIMESTAMP WHERE Ip = @Ip AND Db = @Db AND StartByte = @StartByte AND Length = @Length; ";
                int result = command.ExecuteNonQuery();

                if (result == 0) // Kein Eintrag vorhanden, neuen anlegen
                {
                    command.CommandText =
                    @"INSERT INTO ReadFailure (Ip, Db, StartByte, Length) VALUES (@Ip, @Db, @StartByte, @Length); ";
                    command.ExecuteNonQuery();
                }
            }
        }

        internal static List<Tuple<DateTime, string, string>> GetLogEntries(int limit = 1000)
        {
            List<Tuple<DateTime, string, string>> logEntries = [];
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();
                var query = @"SELECT Time, Category, Message FROM Log ORDER BY Id DESC LIMIT @Limit;";
                command.CommandText = query;
                command.Parameters.AddWithValue("@Limit", limit);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    DateTime time = DateTime.Parse(reader.GetString(0));
                    string category = reader.GetString(1);
                    string message = reader.GetString(2);
                    logEntries.Add(new Tuple<DateTime, string, string>(time, category, message));
                }
                connection.Dispose();
            }
            return logEntries;
        }

        internal static List<ReadFailure> DbLogGetReadFailures()
        {
            List<ReadFailure> failures = [];

            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();
                var query = @"SELECT Ip, Db, StartByte, Length, Time FROM ReadFailure ORDER BY Time DESC;";
                command.CommandText = query;
                using var reader = command.ExecuteReader();
                
                while (reader.Read())
                {
                    failures.Add(new ReadFailure()
                    {
                        Ip = reader.GetString(0),
                        Db = reader.GetInt32(1),
                        StartByte = reader.GetInt32(2),
                        Length = reader.GetInt32(3),
                        Time = DateTime.Parse(reader.GetString(4))
                    });                   
                }
                connection.Dispose();
                return failures;
            }
        }

        internal class ReadFailure
        {
            public string Ip { get; set; } = string.Empty;
            public int Db { get; set; }
            public int StartByte { get; set; }
            public int Length { get; set; }
            public DateTime Time { get; set; }
        }

        #endregion

        #region SPS Quellen verwalten

        internal static CpuType ParseCpuType(string cpuTypeStr)
        {
            return cpuTypeStr.ToUpper() switch
            {
                "S71200" => CpuType.S71200,
                "S71500" => CpuType.S71500,
                "S7300" => CpuType.S7300,
                "S7400" => CpuType.S7400,
                _ => CpuType.S71500,
            };
        }

        internal static Dictionary<string, Plc> SelectActivePlcs()
        {
           lock (_dbLock)
           {
                Dictionary<string, Plc> plcs = [];
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();
                var query = @"SELECT Name, CpuType, Ip, Rack, Slot FROM Source WHERE IsActive > 0;";
                command.CommandText = query;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string name = reader.GetString(0);
                    CpuType cpuType = ParseCpuType(reader.GetString(1));
                    string ipAddress = reader.GetString(2);
                    short rack = reader.GetInt16(3);
                    short slot = reader.GetInt16(4);
                    Plc plc = new(cpuType, ipAddress, rack, slot);
                    plcs.Add(name, plc);
                }
                connection.Dispose();
                return plcs;
            }

            /*
             * CREATE TABLE IF NOT EXISTS Source ( 
                           Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                           Name TEXT NOT NULL UNIQUE,     
                           CpuType STRING NOT NULL DEFAULT 'S71500',  
                           Ip TEXT,                                                     
                           Rack INTEGER DEFAULT 0,
                           Slot INTEGER DEFAULT 0,
                           Comment TEXT                          
                           );
             */
        }

        internal static List<PlcConf> SelectAllPlcs()
        {
            lock (_dbLock)
            {
                List<PlcConf> plcs = [];
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();
                var query = @"SELECT Id, Name, CpuType, Ip, Rack, Slot, IsActive, Comment FROM Source;";
                command.CommandText = query;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    string name = reader.GetString(1);
                    CpuType cpuType = ParseCpuType(reader.GetString(2));
                    string ipAddress = reader.GetString(3);
                    short rack = reader.GetInt16(4);
                    short slot = reader.GetInt16(5);
                    bool isActive = !reader.IsDBNull(6) && reader.GetBoolean(6);
                    string comment = reader.IsDBNull(7) ? string.Empty : reader.GetString(7);
                    PlcConf plc = new(id, name, cpuType, ipAddress, rack, slot, isActive, comment);
                    plcs.Add(plc);
                }
                connection.Dispose();
                return plcs;
            }

            /*
             * CREATE TABLE IF NOT EXISTS Source ( 
                           Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                           Name TEXT NOT NULL UNIQUE,     
                           CpuType STRING NOT NULL DEFAULT 'S71500',  
                           Ip TEXT,                                                     
                           Rack INTEGER DEFAULT 0,
                           Slot INTEGER DEFAULT 0,
                           Comment TEXT                          
                           );
             */
        }

        internal static int CreatePlc(PlcConf plc)
        {
            /*
              Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                          Name TEXT NOT NULL UNIQUE,     
                          CpuType STRING NOT NULL DEFAULT 'S71500',  
                          Ip TEXT,                                                     
                          Rack INTEGER DEFAULT 0,
                          Slot INTEGER DEFAULT 0,
                          IsActive INTEGER DEFAULT 1,
                          Comment TEXT 
             */
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText =
                @"
                    INSERT INTO Source (Name, CpuType, Ip, Rack, Slot, IsActive, Comment) 
                    VALUES (@Name, @CpuType, @Ip, @Rack, @Slot, @IsActive, @Comment); ";
                command.Parameters.AddWithValue("@Name", plc.Name);
                command.Parameters.AddWithValue("@CpuType", plc.CpuType.ToString());
                command.Parameters.AddWithValue("@Ip", plc.Ip);
                command.Parameters.AddWithValue("@Rack", plc.Rack);
                command.Parameters.AddWithValue("@Slot", plc.Slot);
                command.Parameters.AddWithValue("@IsActive", plc.IsActive ? 1 : 0);
                command.Parameters.AddWithValue("@Comment", plc.Comment);
                return command.ExecuteNonQuery();
            }
        }

        internal static int UpdatePlc(PlcConf plc)
        {
            /*
              Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                          Name TEXT NOT NULL UNIQUE,     
                          CpuType STRING NOT NULL DEFAULT 'S71500',  
                          Ip TEXT,                                                     
                          Rack INTEGER DEFAULT 0,
                          Slot INTEGER DEFAULT 0,
                          IsActive INTEGER DEFAULT 1,
                          Comment TEXT 
             */
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();

                command.Parameters.AddWithValue("@Id", plc.Id);
                command.Parameters.AddWithValue("@Name", plc.Name);
                command.Parameters.AddWithValue("@CpuType", plc.CpuType.ToString());
                command.Parameters.AddWithValue("@Ip", plc.Ip);
                command.Parameters.AddWithValue("@Rack", plc.Rack);
                command.Parameters.AddWithValue("@Slot", plc.Slot);
                command.Parameters.AddWithValue("@IsActive", plc.IsActive ? 1 : 0);
                command.Parameters.AddWithValue("@Comment", plc.Comment);

                    //Console.WriteLine("Benutzer geändert mit Passwortänderung.");
                command.CommandText =
                @"  UPDATE Source
                    SET 
                     Name = @Name
                    ,CpuType = @CpuType
                    ,Ip = @Ip
                    ,Rack = @Rack
                    ,Slot = @Slot
                    ,IsActive = @IsActive
                    ,Comment = @Comment
                    WHERE Id = @Id; ";

                return command.ExecuteNonQuery();
            }
        }

        internal static int DeletePlc(int plcId)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();
                command.Parameters.AddWithValue("@Id", plcId);
      
                command.CommandText =
                @"  DELETE FROM Source                    
                    WHERE Id = @Id AND (SELECT COUNT(Id) FROM Source) > 1; "; // die letzt SPS kann nicht gelöscht werden.

                return command.ExecuteNonQuery();
            }
        }
        #endregion

        internal static List<TagCollection> GetTagCollections()
        {
            List<TagCollection> tcs = [];

            lock (_dbLock)
            {
               
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();
                var query = @"SELECT Id, Name, Author, Start, End, Interval, Tags FROM ChartConfig;";
                command.CommandText = query;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    string name = reader.GetString(1);
                    string author = reader.GetString(2);
                    DateTime start = DateTime.Parse(reader.GetString(3));
                    DateTime end = DateTime.Parse(reader.GetString(4));
                    string intervalStr = reader.GetString(5);
                    string tags = reader.GetString(6);
                   // tags = tags.Split("\"value\":")[1].TrimEnd('}');
                    
                        Console.WriteLine( $"Übergebene Tags: {tags}");

                    Tag[] tagArray = System.Text.Json.JsonSerializer.Deserialize(tags, AppJsonSerializerContext.Default.TagArray) ?? [];
                    MiniExcel.Interval interval = MiniExcel.GetTimeFormat(intervalStr);
                    TagCollection tc = new(id, name, author, start, end, (int)interval, tagArray);

              
                        Console.WriteLine($"Lade Tag-Zusammenstellung '{name}'");

                    tcs.Add(tc);
                }
                connection.Dispose();
                
            }


            //JSON.stringify({ id: 0, name: chartName, author: '', start: start.toISOString(), end: end.toISOString(), interval: parseInt(interval), tags: tagNames });
            return tcs;
            //throw new NotImplementedException();
            //return new TagCollection("","",DateTime.MinValue, DateTime.MaxValue, DynContent.MiniExcel.Interval.Jahr, new Tag[] {new Tag("","",0,false)});
        }

        internal static int CreateChartconfig(string chartName, string author, DateTime start, DateTime end, DynContent.MiniExcel.Interval interval, Tag[] tags)
        {
            /*
                   CREATE TABLE IF NOT EXISTS ChartConfig ( 
                          Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                          Name TEXT NOT NULL,                               
                          Author TEXT NOT NULL,  
                          Start TEXT,
                          End TEXT,
                          Interval TEXT,
                          Tags TEXT
                          );
             */

#if DEBUG
            if (_logger?.IsEnabled(LogLevel.Debug) == true)
            {
                _logger.LogDebug("Erstelle ChartConfig: {ChartName}, {Author}, {Start}, {End}, {Interval}, {Tags}", chartName, author, start, end, interval, tags.Length);
            }
#endif
            string tagsJson = System.Text.Json.JsonSerializer.Serialize(tags, AppJsonSerializerContext.Default.TagArray);

            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText =
                @" INSERT INTO ChartConfig (Name, Author, Start, End, Interval, Tags) 
                   VALUES (@Name, @Author, @Start, @End, @Interval, @Tags); ";
                command.Parameters.AddWithValue("@Name", chartName);
                command.Parameters.AddWithValue("@Author", author);
                command.Parameters.AddWithValue("@Start", start.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@End", end.ToString("yyyy-MM-dd HH:mm:ss"));
                command.Parameters.AddWithValue("@Interval", interval.ToString());
                command.Parameters.AddWithValue("@Tags", tagsJson);                
                return command.ExecuteNonQuery();
            }

            //throw new NotImplementedException();
        }


        internal static int DeleteChartconfig(int id)
        {
            throw new NotImplementedException();
        }

    }
}
