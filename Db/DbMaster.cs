using Gemini.DynContent;
using Gemini.Models;
using Microsoft.Data.Sqlite;
using S7.Net;

namespace Gemini.Db
{
    /// <summary>
    /// Benutzerrollen
    /// </summary>
    public enum Role
    {
        /// <summary>
        /// Unbekannt - Standardwert, wenn keine Rolle zugeordnet ist.
        /// </summary>
        Unbekannt,

        /// <summary>
        /// Administrator mit vollen Rechten.
        /// </summary>
        Admin,

        /// <summary>
        /// Registrierter Benutzer.
        /// </summary>
        User,

        /// <summary>
        /// Gastbenutzer mit eingeschränkten Rechten.
        /// </summary>
        Guest
    }

    internal sealed class User 
    {
        public required int Id { get; set; }
        public required string Name { get; set; }
        public required Role Role { get; set; }
    }

    internal sealed class PlcConf
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


    internal sealed partial class Db
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
                var query = @"SELECT Id, Name, RoleId FROM User;";
                command.CommandText = query;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    int userId = reader.GetInt32(0);
                    string userName = reader.GetString(1);
                    Role userRole = (Role)reader.GetInt32(2);                    
                    users.Add(new User(){ Id = userId, Name = userName, Role = userRole});
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
                command.Parameters.Add("@Name", SqliteType.Text).Value = name;
                string passwordHash = Gemini.Middleware.PasswordHasher.HashPassword(password);
                command.Parameters.Add("@Hash", SqliteType.Text).Value = passwordHash;
                command.Parameters.Add("@RoleId", SqliteType.Integer).Value = (int)role;
                return command.ExecuteNonQuery();
            }
        }

        internal static int UpdateUser(int id, string name, string password, Role role = Role.Guest)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();

                command.Parameters.Add("@UserId", SqliteType.Integer).Value = id;
                command.Parameters.Add("@Name", SqliteType.Text).Value = name;
                command.Parameters.Add("@RoleId", SqliteType.Integer).Value = (int)role;

                if (password.IsWhiteSpace())
                {
                    //Console.WriteLine("Benutzer geändert ohne Passwortänderung.");
                    command.CommandText =
                @"  UPDATE User 
                    SET RoleId = @RoleId, Name = @Name
                    WHERE Id = @UserId; ";
                }
                else
                {
                    //Console.WriteLine("Benutzer geändert mit Passwortänderung.");
                    command.CommandText =
                @"  UPDATE User 
                    SET Hash = @Hash, RoleId = @RoleId, Name = @Name 
                    WHERE Id = @UserId; ";

                    string passwordHash = Gemini.Middleware.PasswordHasher.HashPassword(password);
                    command.Parameters.Add("@Hash", SqliteType.Text).Value = passwordHash;
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
                command.Parameters.Add("@Name", SqliteType.Text).Value = name;
                command.Parameters.Add("@AdminRoleId", SqliteType.Integer).Value = (int)Role.Admin;
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

                command.Parameters.Add("@Limit", SqliteType.Integer).Value = limit;
                command.CommandText =
                @" DELETE FROM Log WHERE Id NOT IN (SELECT Id FROM Log ORDER BY Id DESC LIMIT @Limit); ";

                _ = command.ExecuteNonQuery();
            }

        }

        private static async Task DbLogAsync(string category, string message)
        {
            //CreateMasterDatabaseAsync();

            try
            {
                await using var connection = new SqliteConnection(MasterDbSource);
                await connection.OpenAsync();

                var command = connection.CreateCommand();
                command.CommandText =
                    @"INSERT INTO Log (Category, Message) VALUES (@Category, @Message); ";

                command.Parameters.Add("@Category", SqliteType.Text).Value = category;
                command.Parameters.Add("@Message", SqliteType.Text).Value = message;
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                // Fallback zu Konsole oder System.Diagnostics.Debug
                System.Diagnostics.Debug.WriteLine($"DbLog Fehler: {ex.Message}");
                // Optional: Schreibe zu stderr als letzter Ausweg
                Console.Error.WriteLine($"[{DateTime.UtcNow:O}] DbLog Fehler: {ex.Message}");
                _logger?.LogError("Fehler beim Schreiben des Logs in die Datenbank: {ErrorMessage}", ex.Message);
            }
        }

        /// <summary>
        /// Fire-and-forget Logging ohne Fehlerbehandlung für den Aufrufer.
        /// Exceptions werden in DbLogAsync abgefangen und zu stderr geschrieben.
        /// </summary>
        private static void DbLogFireAndForget(string category, string message)
        {
            // Dokumentation: Diese Methode wirft keine Exceptions zurück
            _ = Task.Run(async () => await DbLogAsync(category, message))
                .ConfigureAwait(false);
        }


        internal static void DbLogInfo(string message)
        {
            DbLogFireAndForget("Info", message);
            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation("{Message}", message);
            }
        }

        internal static void DbLogWarn(string message)
        {
            DbLogFireAndForget("Warn", message);

            if (_logger?.IsEnabled(LogLevel.Warning) == true)            
            {
                // CA1873: Vermeide teure Auswertung, wenn Logging deaktiviert ist
                _logger.LogWarning("{Message}", message);            
            }
        }

        internal static void DbLogError(string message)
        {
            DbLogFireAndForget("Error", message);

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

                command.Parameters.Add("@Ip", SqliteType.Text).Value = plcIp;
                command.Parameters.Add("@Db", SqliteType.Integer).Value = db;
                command.Parameters.Add("@StartByte", SqliteType.Integer).Value = startByte;
                command.Parameters.Add("@Length", SqliteType.Integer).Value = length;

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
                command.Parameters.Add("@Limit", SqliteType.Integer).Value = limit;
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
                command.Parameters.Add("@Name", SqliteType.Text).Value = plc.Name;
                command.Parameters.Add("@CpuType", SqliteType.Text).Value = plc.CpuType.ToString();
                command.Parameters.Add("@Ip", SqliteType.Text).Value = plc.Ip;
                command.Parameters.Add("@Rack", SqliteType.Integer).Value = plc.Rack;
                command.Parameters.Add("@Slot", SqliteType.Integer).Value = plc.Slot;
                command.Parameters.Add("@IsActive", SqliteType.Integer).Value = plc.IsActive ? 1 : 0;
                command.Parameters.Add("@Comment", SqliteType.Text).Value = plc.Comment;
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

                command.Parameters.Add("@Id", SqliteType.Integer).Value = plc.Id;
                command.Parameters.Add("@Name", SqliteType.Text).Value = plc.Name;
                command.Parameters.Add("@CpuType", SqliteType.Text).Value = plc.CpuType.ToString();
                command.Parameters.Add("@Ip", SqliteType.Text).Value = plc.Ip;
                command.Parameters.Add("@Rack", SqliteType.Integer).Value = plc.Rack;
                command.Parameters.Add("@Slot", SqliteType.Integer).Value = plc.Slot;
                command.Parameters.Add("@IsActive", SqliteType.Integer).Value = plc.IsActive ? 1 : 0;
                command.Parameters.Add("@Comment", SqliteType.Text).Value = plc.Comment;

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
                command.Parameters.Add("@Id", SqliteType.Integer).Value = plcId;
      
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

                Console.WriteLine($"Übergebene Tags: {tags}");

                //Tag[] tagArray = System.Text.Json.JsonSerializer.Deserialize(tags, AppJsonSerializerContext.Default.TagArray) ?? [];
                ChartConfig? tagArray = System.Text.Json.JsonSerializer.Deserialize(tags, AppJsonSerializerContext.Default.ChartConfig);

                if (tagArray is null)
                {
                    Console.WriteLine($"Fehler beim Deserialisieren der Tags für TagCollection '{name}'. JSON: {tags}");
                    continue; // überspringe diese TagCollection
                }

                MiniExcel.Interval interval = MiniExcel.GetTimeFormat(intervalStr);
                TagCollection tc = new(id, name, author, start, end, (int)interval, tagArray);


                Console.WriteLine($"Lade Tag-Zusammenstellung '{name}'");

                tcs.Add(tc);
            }
            connection.Dispose();

            //JSON.stringify({ id: 0, name: chartName, author: '', start: start.toISOString(), end: end.toISOString(), interval: parseInt(interval), tags: tagNames });
            return tcs;
            //throw new NotImplementedException();
            //return new TagCollection("","",DateTime.MinValue, DateTime.MaxValue, DynContent.MiniExcel.Interval.Jahr, new Tag[] {new Tag("","",0,false)});
        }

        internal static TagCollection? GetTagCollection(int id)
        {
            TagCollection? tc = null;
            using var connection = new SqliteConnection(MasterDbSource);
            connection.Open();
            var command = connection.CreateCommand();
            var query = @"SELECT Id, Name, Author, Start, End, Interval, Tags FROM ChartConfig WHERE Id = @Id;";
            command.Parameters.Add("@Id", SqliteType.Integer).Value = id;
            command.CommandText = query;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {                
                string name = reader.GetString(1);
                string author = reader.GetString(2);
                DateTime start = DateTime.Parse(reader.GetString(3));
                DateTime end = DateTime.Parse(reader.GetString(4));
                string intervalStr = reader.GetString(5);
                string tags = reader.GetString(6);
                // tags = tags.Split("\"value\":")[1].TrimEnd('}');
#if DEBUG
                Console.WriteLine($"Übergebene Tags: {tags}");
#endif
                //Tag[] tagArray = System.Text.Json.JsonSerializer.Deserialize(tags, AppJsonSerializerContext.Default.TagArray) ?? [];

                ChartConfig? tagArray = System.Text.Json.JsonSerializer.Deserialize(tags, AppJsonSerializerContext.Default.ChartConfig);

                if (tagArray is null)
                {
                    Console.WriteLine($"Fehler beim Deserialisieren der Tags für TagCollection '{name}'. JSON: {tags}");
                    continue; // überspringe diese TagCollection
                }

                MiniExcel.Interval interval = MiniExcel.GetTimeFormat(intervalStr);
                tc = new(id, name, author, start, end, (int)interval, tagArray);

                Console.WriteLine($"Lade Tag-Zusammenstellung '{name}'");

                if (tc is not null)
                    break;
            }
            connection.Dispose();

            return tc;
        }

        internal static int CreateChartconfig(string chartName, string author, DateTime start, DateTime end, DynContent.MiniExcel.Interval interval, ChartConfig tags)
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
                _logger.LogDebug("Erstelle ChartConfig: {ChartName}, {Author}, {Start}, {End}, {Interval}, {Tags}", chartName, author, start, end, interval, tags.Chart1Tags.Count);
            }
#endif
            string tagsJson = System.Text.Json.JsonSerializer.Serialize(tags, AppJsonSerializerContext.Default.ChartConfig);

            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText =
                @" INSERT INTO ChartConfig (Name, Author, Start, End, Interval, Tags) 
                   VALUES (@Name, @Author, @Start, @End, @Interval, @Tags); ";
                command.Parameters.Add("@Name", SqliteType.Text).Value = chartName;
                command.Parameters.Add("@Author", SqliteType.Text).Value = author;
                command.Parameters.Add("@Start", SqliteType.Text).Value = start.ToString("yyyy-MM-dd HH:mm:ss");
                command.Parameters.Add("@End", SqliteType.Text).Value = end.ToString("yyyy-MM-dd HH:mm:ss");
                command.Parameters.Add("@Interval", SqliteType.Text).Value = interval.ToString();
                command.Parameters.Add("@Tags", SqliteType.Text).Value = tagsJson;                
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
