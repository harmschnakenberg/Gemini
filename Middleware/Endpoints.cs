using Gemini.Db;
using Gemini.DynContent;
using Gemini.Models;
using Gemini.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using S7.Net;
using System.Drawing.Drawing2D;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Gemini.Middleware
{
    
    public static partial class Endpoints
    {
        internal static bool PleaseStop = false;
        internal static CancellationTokenSource cancelTokenSource = new();

        public static void MapEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("favicon.ico", Favicon).AllowAnonymous();
            app.MapGet("/js/{filename}", JavaScriptFile).AllowAnonymous(); // Statische JS-Dateien dynamisch ausliefern oder über wwwroot?
            app.MapGet("/css/{filename}", StylesheetFile).AllowAnonymous(); // Statische CSS-Dateien dynamisch ausliefern oder über wwwroot?

            app.MapPost("/login", Login).AllowAnonymous();
            app.MapPost("/logout", Logout).RequireAuthorization(); // Logout Endpunkt (Nötig, da Client HttpOnly Cookies nicht löschen kann)
            app.MapGet("/antiforgery/token", RefreshAntiForgeryToken).AllowAnonymous();

            app.MapGet("/user", SelectUsers).RequireAuthorization();
            app.MapPost("/user/create", UserCreate).RequireAuthorization();
            app.MapPost("/user/update", UserUpdate).RequireAuthorization();
            app.MapPost("/user/delete", UserDelete).RequireAuthorization();

            app.MapGet("/source", GetAllPlcConfig).RequireAuthorization();
            app.MapPost("/source/create", PlcCreate).RequireAuthorization();
            app.MapPost("/source/update", PlcUpdate).RequireAuthorization();
            app.MapPost("/source/delete", PlcDelete).RequireAuthorization();
            app.MapPost("/source/ping", PlcPing).RequireAuthorization();

            app.MapGet("/soll/{id:int}", SollMenu).RequireAuthorization(); // Soll-Menü HTML aus JSON-Datei erstellen und ausliefern
            app.MapGet("/chart", Chart).RequireAuthorization(); // Chart HTML ausliefern (bisher statisch, ToDo: TagNames dynamisch übergeben)
            app.MapGet("/db", DbQuery).RequireAuthorization(); // Datenbankabfrage und Ausgabe als JSON            
            app.MapPost("/tagcomments", GetTagComments); // Tag-Kommentare abrufen
            app.MapPost("/tagupdate", TagConfigUpdate); // Tag-Kommentar und Log-Flag aktualisieren
            app.MapGet("/excel", GetExcelForm); // Excel-Export Formular ausliefern
            app.MapPost("/excel", ExcelDownload); // Excel-Datei generieren und ausliefern
            app.MapGet("/all", GetAllTagsConfig).RequireAuthorization(); // Alle Tags mit Kommentaren und Log-Flags als HTML-Tabelle ausliefern
            app.MapGet("/restart", ServerRestart); // Kestrel-Server neu starten
            app.MapGet("/exit", ServerShutdown); // Server herunterfahren
            app.MapGet("/", MainMenu).AllowAnonymous(); // Hauptmenü HTML ausliefern


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

            if (link.EndsWith(".html"))
            {   //statische HTML-Datei ausliefern
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/html";
                var file = File.ReadAllText(link, Encoding.UTF8);
                await ctx.Response.WriteAsync(file);
                await ctx.Response.CompleteAsync();
            }
            else if (link.EndsWith(".svg")) //ToDo: SVG Implementieren
            {   //statische SVG-Datei ausliefern
                var fileBytes = await File.ReadAllBytesAsync(link);
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "image/svg+xml";
                var file = File.ReadAllText(link, Encoding.UTF8);
                await ctx.Response.WriteAsync(file);
                await ctx.Response.CompleteAsync();
            }
            else if (link.EndsWith(".json"))
            {
                string html = await SollPageBuilder.BuildSollPageFromJsonFile(link);
                //dynamisches Erstellen der Sollwert-Seite aus JSON-Datei
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/html";
                await ctx.Response.WriteAsync(html);
                await ctx.Response.CompleteAsync();

            }



        }

        private static async Task Chart(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html";
            var file = File.ReadAllText("wwwroot/html/chart.html", Encoding.UTF8);
            await ctx.Response.WriteAsync(file);
            await ctx.Response.CompleteAsync();
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

        private static async Task ServerRestart(HttpContext ctx)
        {
            StringBuilder sb = new();

            sb.Append(@"<!DOCTYPE html>
                <html lang='de'>
                <head>
                    <meta charset='UTF-8'>
                    <title>Server neu gestartet</title>
                    <link rel='icon' type='image/x-icon' href='/favicon.ico'>
                    <link rel='shortcut icon' href='/favicon.ico'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <link rel='stylesheet' href='/css/style.css'>                    
                </head>
                <body>");

            sb.AppendLine("<h1>Der Server wurde neu gestartet.</h1>");
            sb.AppendLine("<p>-ohne Weiterleitung-</p>");
            sb.Append(@"
                </body>
                </html>");

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html";
            await ctx.Response.WriteAsync(sb.ToString());
            await ctx.Response.CompleteAsync();

            cancelTokenSource.Cancel();
        }

        private static async Task ServerShutdown(HttpContext ctx)
        {
            StringBuilder sb = new();

            sb.Append(@"<!DOCTYPE html>
                <html lang='de'>
                <head>
                    <meta charset='UTF-8'>
                    <title>Server Beendet</title>
                    <link rel='icon' type='image/x-icon' href='/favicon.ico'>
                    <link rel='shortcut icon' href='/favicon.ico'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <link rel='stylesheet' href='/css/style.css'>                    
                </head>
                <body>");

            sb.AppendLine("<h1>Der Server wurde beendet.</h1>");
            sb.AppendLine("<p>Neustart nur über die Console möglich.</p>");
            sb.Append(@"
                </body>
                </html>");

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html";
            await ctx.Response.WriteAsync(sb.ToString());
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

        private class SollPageBuilder
        {
            /// <summary>
            /// TODO: Implement this method
            /// Asynchronously builds a SOLL page by processing the JSON file located at the specified link.
            /// </summary>
            /// <param name="link">The URL or file path to the JSON file containing the data required to construct the SOLL page. Cannot be
            /// null or empty.</param>
            /// <returns>A string containing the generated SOLL page content based on the provided JSON file.</returns>
            /// <exception cref="NotImplementedException">Thrown in all cases as the method is not yet implemented.</exception>
            internal static async Task<string> BuildSollPageFromJsonFile(string linkToJson)
            {
                StringBuilder sb = new();

                sb.Append(@"<!DOCTYPE html>
                <html lang='de'>
                <head>
                    <meta charset='UTF-8'>
                    <title>Datenquellen</title>                    
                    <link rel='shortcut icon' href='/favicon.ico'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <link rel='stylesheet' href='/css/style.css'>                    
                    <script src='/js/websocket.js'></script>
                </head>
                <body>");


                string json;                
                using (TextReader reader = new StreamReader(linkToJson))
                {
                    json = await reader.ReadToEndAsync();
                };
#if DEBUG
                Console.WriteLine(json);
#endif
                SollwertFromJson[]? sollList = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.SollwertFromJsonArray);

                if (sollList == null)
                {
#if DEBUG
                    Console.WriteLine("BuildSollPageFromJsonFile() Konnte nicht geparsed werden: " + json);
#endif
                    return string.Empty;
                }

                foreach (var item in sollList)
                {


                   // item.Comment
                }

                throw new NotImplementedException();
            }
        }
    }
}
