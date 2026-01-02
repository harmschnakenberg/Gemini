using Microsoft.Data.Sqlite;

namespace Gemini.Db
{

    internal class User
    {
        public required string Name { get; set; }
        public required string Role { get; set; }
    }

    internal partial class Db
    {
          
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

        internal static List<User> SelectAllUsers()
        {
            List<User> users = [];

            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();
                var query = @"SELECT Name, Role FROM User;";
                command.CommandText = query;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string userName = reader.GetString(0);
                    string userRole = reader.GetString(1);
                    users.Add(new User { Name = userName, Role = userRole });
                }
                connection.Dispose();
            }
            return users;
        }

        internal static string GetUserRole(string username)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();
                var nameParam = command.Parameters.Add("@Name", SqliteType.Text);
                var query = @"SELECT Role FROM User WHERE Name = @Name;";
                command.CommandText = query;
                nameParam.Value = username;
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string userRole = reader.GetString(0);
                    return userRole;
                }
                connection.Dispose();
            }
            return "Unknown";
        }

        internal static void DeleteUser(string name)
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText =
                @"
                    DELETE FROM User WHERE Name = @Name AND 0 < (SELECT COUNT(Role) WHERE Role == 'Admin'); "; // ToDo: Testen, ob noch ein Admin übrig bleibt
                command.Parameters.AddWithValue("@Name", name);
                command.ExecuteNonQuery();
            }
        }


        internal static int CreateUser(string name, string password, string role = "User")
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText =
                @"
                    INSERT INTO User (Name, Hash, Role) 
                    VALUES (@Name, @Hash, @Role); ";
                command.Parameters.AddWithValue("@Name", name);
                string passwordHash = Gemini.Middleware.PasswordHasher.HashPassword(password);
                command.Parameters.AddWithValue("@Hash", passwordHash);
                command.Parameters.AddWithValue("@Role", role);
                return command.ExecuteNonQuery();
            }
        }


        internal static bool AuthenticateUser(string username, string userpassword, string requiredUserRole = "User")
        {
            lock (_dbLock)
            {
                
                using var connection = new SqliteConnection(MasterDbSource);
                connection.Open();
                
                var command = connection.CreateCommand();
                var nameParam = command.Parameters.Add("@Name", SqliteType.Text);
                var pswdParam = command.Parameters.Add("@Password", SqliteType.Text);

                var query = @"SELECT Hash, Role FROM User WHERE Name = @Name;";
                command.CommandText = query;          
                nameParam.Value = username;
                
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {                    
                    string storedHash = reader.GetString(0);                    
                    //Console.WriteLine($"Vergleich {userpassword}\t{storedHash}  {Gemini.Middleware.PasswordHasher.HashPassword(userpassword)}");
                    bool passwordMatch = Gemini.Middleware.PasswordHasher.VerifyPassword(userpassword, storedHash);
                    
                    string userRole = reader.GetString(1);
                    bool userRoleOk = userRole.Equals(requiredUserRole, StringComparison.OrdinalIgnoreCase);

                    return passwordMatch && (requiredUserRole.Equals("user", StringComparison.OrdinalIgnoreCase) || userRoleOk);
                }

                connection.Dispose();
            }
                return false;
        }

    }
}
