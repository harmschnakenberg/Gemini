using Gemini.Db;
using Gemini.DynContent;
using Gemini.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace Gemini.Middleware
{
    public static partial class Endpoints
    {
        private static IResult UserCreate(HttpContext ctx, ClaimsPrincipal user)
        {
            bool isAdmin = user.IsInRole("Admin");
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

            bool isAdmin = user.IsInRole("Admin");
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
            bool isAdmin = user.IsInRole("Admin");
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
}
