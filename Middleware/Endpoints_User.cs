using Gemini.Db;
using Gemini.DynContent;
using Gemini.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;


namespace Gemini.Middleware
{
    public static partial class Endpoints
    {
        private static IResult UserCreate(HttpContext ctx, ClaimsPrincipal user)
        {
            bool isAdmin = user.IsInRole(Role.Admin.ToString());
            if (!isAdmin) // Nur Admins können Benutzer erstellen
            {
                Db.Db.DbLogInfo($"Keine Berechtigung {user.Identity?.Name} [{user.Claims?.FirstOrDefault()?.Value}]");
                return Results.Unauthorized();
            }

            string name = ctx.Request.Form["name"].ToString() ?? string.Empty;
            string role = ctx.Request.Form["role"].ToString() ?? string.Empty;
            string pwd = ctx.Request.Form["pwd"].ToString() ?? string.Empty;

            Console.WriteLine($"Änderung für name: {name}, role: {role}, pwd:{pwd} von {user.Identity?.Name} [{user.Claims?.FirstOrDefault()?.Value}]");

            int result = Db.Db.CreateUser(name, pwd, Enum.Parse<Role>(role));
            //Console.WriteLine($"UserCreate DatenbankQuery Result = " + result);

            if (result > 0)
                return SelectUsers(user);
            else
                return Results.InternalServerError();
        }

        private static IResult UserUpdate(HttpContext ctx, ClaimsPrincipal user)
        {
            string name = ctx.Request.Form["name"].ToString() ?? string.Empty;
            string role = ctx.Request.Form["role"].ToString() ?? string.Empty;
            string pwd = ctx.Request.Form["pwd"].ToString() ?? string.Empty;

            bool isAdmin = user.IsInRole(Role.Admin.ToString());
            bool isCurrentUser = user.Identity?.Name == name;

            if (!isAdmin && !isCurrentUser) //Benutzer können sich nur selbst ändern 
            {
                Console.WriteLine($"Benutzer {name} ändern: Keine Berechtigung {user.Identity?.Name} [{user.Claims?.FirstOrDefault()?.Value}]");
                return Results.Unauthorized();
            }

            int result = Db.Db.UpdateUser(name, pwd, Enum.Parse<Role>(role));
            Console.WriteLine($"UserUpdate DatenbankQuery Result = " + result);


            if (result > 0)
                return Results.Ok();
            else
            {
                Console.WriteLine($"Fehler bei Benutzer {name} ändern durch {user.Identity?.Name} [{user.Claims?.FirstOrDefault()?.Value}] (SQL)");
                return Results.InternalServerError();
            }
        }

        private static IResult UserDelete(HttpContext ctx, ClaimsPrincipal user)
        {
            bool isAdmin = user.IsInRole(Role.Admin.ToString());
            string name = ctx.Request.Form["name"].ToString() ?? string.Empty;
            
            Console.WriteLine($"Benutzer {name} löschen durch {user.Identity?.Name} [{user.Claims?.FirstOrDefault()?.Value}]");
            if (!isAdmin) // Nur Admins können Benutzer löschen
            {
                Console.WriteLine($"Benutzer {name} löschen: Keine Berechtigung {user.Identity?.Name} [{user.Claims?.FirstOrDefault()?.Value}]");
                return Results.Unauthorized();
            }

            int result = Db.Db.DeleteUser(name);
            Console.WriteLine("Delete Result = " + result);

            if (result > 0)
                return Results.Ok();
            else
                return Results.InternalServerError();
        }

        private static IResult SelectUsers(ClaimsPrincipal user)
        {
            List<User> users = Db.Db.SelectAllUsers();
            return Results.Content(HtmlHelper.ListAllUsers(users, user), "text/html");
        }


        private async static Task<IResult> Login(IAntiforgery antiforgery, LoginRequest request, HttpContext context)
        {


            if (Db.Db.AuthenticateUser(request.Username, request.Password, out Role userRole))
            {
#if DEBUG
                Console.WriteLine($"Anmeldung {request.Username}");
#endif
                // A. User einloggen (Setzt das Auth-Cookie)
                var claims = new List<Claim> {
                    new(ClaimTypes.Name, request.Username),
                    new(ClaimTypes.Role, userRole.ToString())
                };
                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await context.SignInAsync(new ClaimsPrincipal(claimsIdentity));

                // B. AntiForgery Token generieren und CSRF-Cookie setzen
                // Das ist entscheidend: Der Client bekommt das Token für den NÄCHSTEN Request.
                var tokens = antiforgery.GetAndStoreTokens(context);

                return Results.Ok(new LoginResponse(tokens.RequestToken!));
            }

            return Results.Unauthorized();
        }

        private static IResult Logout(HttpContext context)
        {
            context.SignOutAsync();
            return Results.Ok(new { Message = "Ausgeloggt" });
        }

        private static IResult RefreshAntiForgeryToken(IAntiforgery antiforgery, HttpContext context)
        {
            // Generiert neue Tokens basierend auf dem aktuellen Auth-Status
            // und setzt das Cookie im Response Header neu.
            var tokens = antiforgery.GetAndStoreTokens(context);

            return Results.Ok(new CsrfTokenResponse(tokens.RequestToken!));
        }

      
    }

    #region // Hilfsklassen für Login/Logout/CSRF Token Refresh

    // --- AOT-freundliche Password Hashing Utility Klasse ---
    public class PasswordHasher
    {
        // Parameter gemäß aktuellen Sicherheitsstandards 2025
        private const int SaltSize = 16; // 128 Bit
        private const int KeySize = 32;  // 256 Bit
        private const int Iterations = 600000;
        private static readonly HashAlgorithmName HashAlgorithm = HashAlgorithmName.SHA512;

        public static string HashPassword(string password)
        {
            // 1. Zufälligen Salt generieren
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);

            // 2. Hash berechnen
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Iterations,
                HashAlgorithm,
                KeySize);

            // 3. Salt und Hash kombiniert als Base64 speichern (getrennt durch Punkt)
            return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        public static bool VerifyPassword(string password, string storedHash)
        {
            // 1. Gespeicherten Salt und Hash extrahieren
            var parts = storedHash.Split('.');
            byte[] salt = Convert.FromBase64String(parts[0]);
            byte[] hash = Convert.FromBase64String(parts[1]);

            // 2. Neues Hash-Ergebnis mit demselben Salt berechnen
            byte[] inputHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                Iterations,
                HashAlgorithm,
                KeySize);

            // 3. Zeitkonstanter Vergleich (schützt vor Timing-Attacks)
            return CryptographicOperations.FixedTimeEquals(hash, inputHash);
        }
    }

    #endregion

}
