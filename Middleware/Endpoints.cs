using Gemini.Db;
using Gemini.DynContent;
using Gemini.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Gemini.Middleware
{
    
    public static class Endpoints
    {
        internal static bool PleaseStop = false;
        
        public static void MapEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/login", Login).AllowAnonymous();
            app.MapPost("/logout", Logout).RequireAuthorization(); // Logout Endpunkt (Nötig, da Client HttpOnly Cookies nicht löschen kann)
            app.MapGet("/antiforgery/token", RefreshAntiForgeryToken).AllowAnonymous();

            app.MapGet("/user", SelectUsers).RequireAuthorization();
            app.MapPost("/user/create", UserCreate);

            app.MapGet("favicon.ico", Favicon).AllowAnonymous();
            app.MapGet("/js/{filename}", JavaScriptFile).AllowAnonymous(); // Statische JS-Dateien ausliefern
            app.MapGet("/css/{filename}", StylesheetFile).AllowAnonymous(); // Statische CSS-Dateien ausliefern
            app.MapGet("/soll/{id:int}", SollMenu).RequireAuthorization(); // Soll-Menü HTML aus JSON-Datei erstellen und ausliefern
            app.MapGet("/chart", Chart).RequireAuthorization(); // Chart HTML ausliefern (bisher statisch, ToDo: TagNames dynamisch übergeben)
            app.MapGet("/db", DbQuery).RequireAuthorization(); // Datenbankabfrage und Ausgabe als JSON            
            app.MapPost("/tagcomments", GetTagComments); // Tag-Kommentare abrufen
            app.MapPost("/tagupdate", TagConfigUpdate); // Tag-Kommentar und Log-Flag aktualisieren
            app.MapGet("/excel", GetExcelForm); // Excel-Export Formular ausliefern
            app.MapPost("/excel", ExcelDownload); // Excel-Datei generieren und ausliefern
            app.MapGet("/all", GetAllTagsConfig).RequireAuthorization(); // Alle Tags mit Kommentaren und Log-Flags als HTML-Tabelle ausliefern
            app.MapGet("/exit", ServerShutdown); // Server herunterfahren
            app.MapGet("/", MainMenu).AllowAnonymous(); // Hauptmenü HTML ausliefern


        }

        private static IResult UserCreate(HttpContext ctx)
        {           
            string name = ctx.Request.Form["name"].ToString() ?? string.Empty;
            string role = ctx.Request.Form["role"].ToString() ?? string.Empty;
            string pwd = ctx.Request.Form["pwd"].ToString() ?? string.Empty;
           
            int result = Db.Db.CreateUser(name, pwd, role);
            Console.WriteLine($"UserCreate DatenbankQuery Result = " + result);

            return Results.Ok(new { Message = $"Neuer Benutzer {name} in die Datenbank eingefügt.", Timestamp = DateTime.Now });
        }

        private static IResult SelectUsers(ClaimsPrincipal user)
        {
            var username = user.Identity?.Name ?? "Unbekannt";
            string role = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value.ToLower() ?? "Unbekannt";
            bool isAdmin = role.Equals("admin");

            if (isAdmin)
            {
                List<User> users = Db.Db.SelectAllUsers();
                return Results.Content(HtmlHelper.ListAllUsers(users), "text/html");
            }
            else
                return Results.Ok(new { Message = $"Hallo {username}, du bist als {role} eingeloggt!", Timestamp = DateTime.Now });

        }


        private async static Task<IResult> Login(IAntiforgery antiforgery, LoginRequest request, HttpContext context)
        {
            

            if (Db.Db.AuthenticateUser(request.Username, request.Password, out string userRole))
            {
#if DEBUG
                Console.WriteLine($"Anmeldung {request.Username}");
#endif
                // A. User einloggen (Setzt das Auth-Cookie)
                var claims = new List<Claim> { 
                    new(ClaimTypes.Name, request.Username), 
                    new(ClaimTypes.Role, userRole)
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

        private static async Task Favicon(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "image/x-icon";
            var file = File.ReadAllText($"wwwroot/favicon.ico", Encoding.UTF8);
            await ctx.Response.WriteAsync(file);
            await ctx.Response.CompleteAsync();
        }

        private static async Task JavaScriptFile(string filename, HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/javascript";
            var file = File.ReadAllText($"wwwroot/js/{filename}", Encoding.UTF8);
            await ctx.Response.WriteAsync(file);
            await ctx.Response.CompleteAsync();
        }

        private static async Task StylesheetFile(string filename, HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/css";
            var file = File.ReadAllText($"wwwroot/css/{filename}", Encoding.UTF8);
            await ctx.Response.WriteAsync(file);
            await ctx.Response.CompleteAsync();
        }

        private static async Task SollMenu(int id, HttpContext ctx)
        {
            string json;
            using (TextReader reader = new StreamReader("wwwroot/js/sollmenu.json"))
            {
                json = await reader.ReadToEndAsync();
            };

            //Console.WriteLine(json);

            var menuTree = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.DictionaryStringMenuLinkArray);

            if (menuTree == null)
                return;

            MenuLink? menuLink = menuTree.First().Value?.Where(i => i.Id == id).FirstOrDefault();
            string link = $"wwwroot/html/soll/{menuLink?.Link ?? string.Empty}";
            if (id == 0 || string.IsNullOrEmpty(menuLink?.Link))
                link = "wwwroot/html/menu.html";

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html";
            var file = File.ReadAllText(link, Encoding.UTF8);
            await ctx.Response.WriteAsync(file);
            await ctx.Response.CompleteAsync();
        }

        private static async Task Chart(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html";
            var file = File.ReadAllText("wwwroot/html/chart.html", Encoding.UTF8);
            await ctx.Response.WriteAsync(file);
            await ctx.Response.CompleteAsync();
        }

        private static async Task DbQuery(HttpContext ctx)
        {
            string[]? tagNames = [];
            DateTime startUtc = DateTime.UtcNow.AddHours(-8);
            DateTime endUtc = DateTime.UtcNow;

            //Console.WriteLine($"DB Request received with query: {ctx.Request.QueryString}");


            if (ctx.Request.Query.TryGetValue("tagnames", out var tagNamesStr))
                tagNames = tagNamesStr.ToString().Split(',');

            if (ctx.Request.Query.TryGetValue("start", out var startStr) && DateTime.TryParse(startStr, out DateTime s))
            {
                startUtc = s.ToUniversalTime(); //lokale Zeit in UTC
                                                //Console.WriteLine($"Parsed start time {startStr} to {start}");
            }

            if (ctx.Request.Query.TryGetValue("end", out var endStr) && DateTime.TryParse(endStr, out DateTime e))
            {
                endUtc = e.ToUniversalTime();
                //Console.WriteLine($"Parsed end time {endStr} to {end}");
            }

            //Console.WriteLine($"DB Request for tags: {string.Join(", ", tagNames!)} from {start} to {end}");
            JsonTag[] obj = await Db.Db.GetDataSet(tagNames!, startUtc, endUtc);
#if DEBUG
            Console.WriteLine($"JsonTag Objekte zum Senden: {obj.Length}");
#endif
            // Console.WriteLine($"Sende {JsonSerializer.Serialize(obj, AppJsonSerializerContext.Default.JsonTagArray)}");
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsJsonAsync(obj, AppJsonSerializerContext.Default.JsonTagArray);
            await ctx.Response.CompleteAsync();
        }

        private static async Task<IResult> GetTagComments()
        {
            List<Tag> allTags = await Db.Db.GetDbTagNames(DateTime.UtcNow, 3);

            List<JsonTag> result = [];

            foreach (var tag in allTags)
            {
               // Console.WriteLine($"{tag.TagName} = {tag.TagValue}");
                result.Add(new JsonTag(tag.TagName, tag.TagComment, DateTime.Now));
            }

            return Results.Json([.. result], AppJsonSerializerContext.Default.JsonTagArray);
        }

        private static IResult TagConfigUpdate(HttpContext ctx, ClaimsPrincipal claimsPrincipal) //, IAntiforgery antiforgery
        {
            bool isAdmin = claimsPrincipal.IsInRole("Admin");
            string userName = claimsPrincipal.Identity?.Name ?? "unbekannt";

            string tagName = ctx.Request.Form["tagName"].ToString() ?? string.Empty;
            string tagComm = ctx.Request.Form["tagComm"].ToString() ?? string.Empty;
            string tagChck = ctx.Request.Form["tagChck"].ToString() ?? string.Empty;
            _ = bool.TryParse(tagChck, out bool isChecked);

            Console.WriteLine($"{userName} veranlasst Tag-Update: {tagName}: {tagComm} | Log {isChecked}");

            if (isAdmin)
            {
                Db.Db.TagUpdate(tagName, tagComm, isChecked);
                return Results.Ok();
            }
            else
                return Results.Unauthorized();
        }

        private static async Task GetExcelForm(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html";
            var file = File.ReadAllText("wwwroot/html/excel.html", Encoding.UTF8);
            await ctx.Response.WriteAsync(file);
            await ctx.Response.CompleteAsync();
        }

        private static async Task ExcelDownload(HttpContext ctx)
        {
            string jsonString = ctx.Request.Form["tags"].ToString() ?? string.Empty;
            //Console.WriteLine("/excel : " + jsonString);

            if (
            !DateTime.TryParse(ctx.Request.Form["start"], out DateTime start) ||
            !DateTime.TryParse(ctx.Request.Form["end"], out DateTime end) ||
            !int.TryParse(ctx.Request.Form["interval"], out int interval) ||
            jsonString?.Length < 3
            )
            {
                string msg = $"Mindestens ein Übergabeparameter war nicht korrekt.\r\n";

#if DEBUG
                msg +=
                $"start: '{ctx.Request.Form["start"]}'\r\n" +
                $"end: '{ctx.Request.Form["end"]}'\r\n" +
                $"interval: '{ctx.Request.Form["interval"]}'\r\n" +
                $"tags: '{ctx.Request.Form["tags"]}'\r\n";
#endif
                ctx.Response.ContentType = "text/plain";
                await ctx.Response.WriteAsync(msg);
                await ctx.Response.CompleteAsync();
                return;
            }

            //Console.WriteLine($"Rohempfang:\r\n'{jsonString}'\r\n");

            JsonTag[] tags = JsonSerializer.Deserialize(jsonString ?? string.Empty, AppJsonSerializerContext.Default.JsonTagArray) ?? [];
            Dictionary<string, string> tagsAndCommnets = tags.ToDictionary(t => t?.N ?? string.Empty, t => t.V?.ToString() ?? string.Empty);
            string[] tagNames = [.. tagsAndCommnets.Keys];

            Console.WriteLine($"Interval = {interval}");

            //JsonTag[] obj = await Db.GetDataSet2(tagNames!, start, end);
            //MemoryStream fileStream = await Excel.CreateExcelWb((Excel.Interval)interval, tagsAndCommnets, obj);

            JsonTag[] obj = await Db.Db.GetDataSet(tagNames!, start, end);
            MemoryStream fileStream = Gemini.DynContent.MiniExcel.DownloadExcel((Gemini.DynContent.MiniExcel.Interval)interval, tagsAndCommnets, obj);

            string excelFileName = $"Werte_{start:yyyyMMdd}_{end:yyyyMMdd}_{interval}_{DateTime.Now.TimeOfDay.TotalSeconds:0000}.xlsx";

            ctx.Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            ctx.Response.Headers.ContentDisposition = $"attachment; filename={excelFileName}";
            ctx.Response.ContentLength = fileStream.Length;

            await fileStream.CopyToAsync(ctx.Response.Body);
            await ctx.Response.CompleteAsync();
        }

        private static async Task GetAllTagsConfig(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html";
            await ctx.Response.WriteAsync(await HtmlHelper.ListAllTags());
            await ctx.Response.CompleteAsync();
        }

        private static async Task ServerShutdown(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html";
            await ctx.Response.WriteAsync(HtmlHelper.ExitForm());
            await ctx.Response.CompleteAsync();

            PleaseStop = true;
        }

        private static async Task MainMenu(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html";
            var file = File.ReadAllText("wwwroot/html/menu.html", Encoding.UTF8);
            await ctx.Response.WriteAsync(file);
            await ctx.Response.CompleteAsync();
        }

    }
}
