using System.Security.Cryptography;
using System.Text;

namespace Gemini.Middleware
{


    //// --- AOT-freundliche Password Hashing Utility Klasse ---
    //public class PasswordHasher
    //{
    //    // Parameter gemäß aktuellen Sicherheitsstandards 2025
    //    private const int SaltSize = 16; // 128 Bit
    //    private const int KeySize = 32;  // 256 Bit
    //    private const int Iterations = 600000;
    //    private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA512;

    //    public static string HashPassword(string password)
    //    {
    //        // 1. Zufälligen Salt generieren
    //        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

    //        // 2. Hash berechnen
    //        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
    //            Encoding.UTF8.GetBytes(password),
    //            salt,
    //            Iterations,
    //            HashAlgorithm,
    //            KeySize);

    //        // 3. Salt und Hash kombiniert als Base64 speichern (getrennt durch Punkt)
    //        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    //    }

    //    public static bool VerifyPassword(string password, string storedHash)
    //    {
    //        // 1. Gespeicherten Salt und Hash extrahieren
    //        var parts = storedHash.Split('.');
    //        byte[] salt = Convert.FromBase64String(parts[0]);
    //        byte[] hash = Convert.FromBase64String(parts[1]);

    //        // 2. Neues Hash-Ergebnis mit demselben Salt berechnen
    //        byte[] inputHash = Rfc2898DeriveBytes.Pbkdf2(
    //            Encoding.UTF8.GetBytes(password),
    //            salt,
    //            Iterations,
    //            HashAlgorithm,
    //            KeySize);

    //        // 3. Zeitkonstanter Vergleich (schützt vor Timing-Attacks)
    //        return CryptographicOperations.FixedTimeEquals(hash, inputHash);
    //    }
    //}
}
