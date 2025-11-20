using Microsoft.Data.Sqlite;

namespace Gemini.Db
{
    internal partial class Db
    {
        #region Logging

        internal static async void DbLogInfo(string message)
        {
            await using var connection = new SqliteConnection(MasterDbSource);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                @"INSERT INTO Log (Category, Message) VALUES ('Info', @Message); ";

            command.Parameters.AddWithValue("@Message", message);
            await command.ExecuteNonQueryAsync();
        }

        #endregion


        #region Benutzerverwaltung

        /// <summary>
        /// Verschlüsselt ein Passwort mit SHA256
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        private static string Encrypt(string password)
        {
            if (password == null) return string.Empty;

            byte[] data = System.Text.Encoding.UTF8.GetBytes(password);

            ///TODO: Salting oder andere Sicherheitsverbesserungen nachpflegen

            data = System.Security.Cryptography.SHA256.HashData(data);
#if DEBUG
            Console.WriteLine($"Passwort '{password}' -> '{System.Text.Encoding.UTF8.GetString(data)}'");
#endif
            return System.Text.Encoding.UTF8.GetString(data);
        }

        #endregion
    }
}
