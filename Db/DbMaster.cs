using Microsoft.Data.Sqlite;

namespace Gemini.Db
{


    #region Benutzerverwaltung 

    public enum Role
    {
        Unbekannt,
        Admin,
        User
    }

    internal class User 
    {
        public required string Name { get; set; }
        public required Role Role { get; set; }
    }


    internal partial class Db
    {
  
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

        internal static int CreateUser(string name, string password, Role role = Role.User)
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

        internal static int UpdateUser(string name, string password, Role role = Role.User)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();

                command.Parameters.AddWithValue("@Name", name);
                command.Parameters.AddWithValue("@RoleId", (int)role);

                if (password.IsWhiteSpace())
                    command.CommandText =
                @"  UPDATE User 
                    SET RoleId = @RoleId
                    WHERE Name = @Name; ";
                else
                {
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

        internal static void DeleteUser(string name)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText =
                $@" DELETE FROM User 
                    WHERE Name = @Name AND 
                    (SELECT COUNT(RoleId) FROM User WHERE RoleId = {(int)Role.Admin}) > 1; "; // ein Admin muss immer übrig bleiben
                command.Parameters.AddWithValue("@Name", name);
                command.ExecuteNonQuery();
            }
        }

     
        #endregion

        #region Logging

        internal static async void DbLogInfo(string message)
        {
            CreateMasterDatabaseAsync();

            await using var connection = new SqliteConnection(MasterDbSource);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                @"INSERT INTO Log (Category, Message) VALUES ('Info', @Message); ";

            command.Parameters.AddWithValue("@Message", message);
            await command.ExecuteNonQueryAsync();
        }

        #endregion
    }
}
