using Gemini.Db;
using Gemini.DynContent;
using Gemini.Models;
using Gemini.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using S7.Net;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Net;
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
            //app.MapGet("favicon.ico", Favicon) .AllowAnonymous();
            //app.MapGet("/js/{filename}", JavaScriptFile).AllowAnonymous(); // Statische JS-Dateien dynamisch ausliefern oder über wwwroot?
            //app.MapGet("/css/{filename}", StylesheetFile).AllowAnonymous(); // Statische CSS-Dateien dynamisch ausliefern oder über wwwroot?

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

            app.MapGet("/tag/all", GetAllTagsConfig).RequireAuthorization(); // Alle Tags mit Kommentaren und Log-Flags als HTML-Tabelle ausliefern
            app.MapGet("/tag/failures", TagReadFailes);
            app.MapPost("/tag/comments", GetTagComments); // Tag-Kommentare abrufen
            app.MapPost("/tag/update", TagConfigUpdate); // Tag-Kommentar und Log-Flag aktualisieren            
            app.MapPost("/tag/write", WriteTagValue).RequireAuthorization();

            app.MapGet("/excel", GetExcelForm); // Excel-Export Formular ausliefern
            app.MapPost("/excel", ExcelDownload); // Excel-Datei generieren und ausliefern
            app.MapPost("/excel/config/create", ExcelConfCreate); // Excel-Konfiguration erstellen
            app.MapPost("/excel/config/delete", ExcelConfDelete); // Excel-Konfiguration löschen //nicht implementiert  
            app.MapGet("/excel/config/all", GetExcelConf);
   
            app.MapGet("/db", DbQuery).RequireAuthorization(); // Datenbankabfrage und Ausgabe als JSON            
            app.MapPost("/db/download", DbDownload); // Datenbank-Dateien ausliefern   
            app.MapGet("/db/list", DbList).RequireAuthorization();
          
            app.MapGet("/soll", SollMenu);
            app.MapGet("/soll/history", GetAlterations);
            app.MapGet("/soll/{id:int}", SollMenu).RequireAuthorization(); // Soll-Menü HTML aus JSON-Datei erstellen und ausliefern
            app.MapGet("/chart", Chart).RequireAuthorization(); // Chart HTML ausliefern (bisher statisch, ToDo: TagNames dynamisch übergeben)
            app.MapGet("/chart/{chartId:int}", DynChart).RequireAuthorization();

            app.MapGet("/log", ShowLog); // Server Log

           // app.MapGet("/cert/download", CertDownload); // SSL-Zertifikat herunterladen (für Windows, ToDo: Linux)

            app.MapGet("/exit", ServerShutdown); // Server herunterfahren
            
            //app.MapGet("/{filePath:file}", ServeStaticFile).AllowAnonymous(); // Statische JS-Dateien dynamisch ausliefern | Offenbar statisch über wwwroot?
            app.MapGet("/", MainMenu).AllowAnonymous(); // Hauptmenü HTML ausliefern


        }

        /// <summary>
        /// Generates a dynamic chart page based on the specified chart ID and associated tag data.
        /// </summary>
        /// <remarks>The method retrieves the chart ID from the route values and uses it to fetch relevant
        /// tag data, which is then used to create a customized chart page. If the chart ID is not valid, a default
        /// chart configuration is used.</remarks>
        /// <param name="context">The HTTP context containing the request information, including route values used to retrieve the chart ID.</param>
        /// <returns>An IResult containing the generated HTML for the chart page, with a content type of 'text/html'.</returns>
        private static IResult DynChart(int chartId, HttpContext context)
        {

            //Console.WriteLine("\r\n"+JsonSerializer.Serialize(chartConfig2, AppJsonSerializerContext.Default.ChartConfig));
            //{"Id":0,"Caption":"Chart 0","SubCaption":"Dynamisch generiertes Chart mit ID 0","Chart1Tags":{"A01_DB10_DBW4":"Minute","A01_DB10_DBW2":"Stunde"},"Chart2Tags":{"A01_DB10_DBX7_3":"Sekunde Bit4"}}

            string json;

            using (TextReader reader = new StreamReader($"wwwroot/html/chart/chart{chartId}.json"))
            {
                json = reader.ReadToEndAsync().Result;
            };

            ChartConfig? chartConfig = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.ChartConfig);

            if (chartConfig is null)
                return Results.Content($"<h1>Ungültige Chart-Konfiguration für ID: {chartId}</h1>", "text/html", Encoding.UTF8);

           return Results.Content(HtmlHelper.DynChart(chartConfig), "text/html", Encoding.UTF8);
        }

        private static IResult ShowLog(HttpContext context)
        {
            List<Tuple<DateTime, string,string>> logEntries = Db.Db.GetLogEntries(1000);

            StringBuilder sb = new();

            sb.Append(@"<!DOCTYPE html>
                <html lang='de'>
                <head>
                    <meta charset='UTF-8'>
                    <title>Server Log</title>
                    <link rel='icon' type='image/x-icon' href='/favicon.ico'>
                    <link rel='shortcut icon' href='/favicon.ico'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <link rel='stylesheet' href='/css/style.css'>    
                    <script src='/js/websocket.js'></script>   
                </head>
                <body>");

            sb.AppendLine("<h1>Server Log</h1>");
            sb.AppendLine("<table class='datatable'>");
            sb.AppendLine("<tr><th>Zeit</th><th>Level</th><th>Nachricht</th></tr>");
            sb.AppendLine("<tbody>");
          
            foreach (var entry in logEntries)            
                sb.AppendLine($"<tr><td style='width:20rem;'>{entry.Item1.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")}</td><td>{entry.Item2}</td><td>{entry.Item3}</td></tr>");
            
            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");

            sb.Append(@"
                </body>
                </html>");

            return Results.Content(sb.ToString(), "text/html", Encoding.UTF8);
        }
             
        private static IResult SollMenu(int id, HttpContext ctx)
        {
            string json;
            using (TextReader reader = new StreamReader("wwwroot/soll/sollmenu.json"))
            {
                json = reader.ReadToEndAsync().Result;
            };

            //Console.WriteLine(json);
            string link = "wwwroot/html/menu.html";

           Dictionary<string, MenuLink[]>? menuTree = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.DictionaryStringMenuLinkArray);

            if (menuTree != null)
            {
                MenuLink? menuLink = menuTree.First().Value?.Where(i => i.Id == id).FirstOrDefault();
                if (id != 0 && !string.IsNullOrEmpty(menuLink?.Link))
                    link = $"wwwroot/html/soll/{menuLink?.Link ?? string.Empty}";
            }
           
            if (link.EndsWith(".html"))
            {   //statische HTML-Datei ausliefern   
                var file = File.ReadAllText(link, Encoding.UTF8);
                return Results.Content(file, "text/html", Encoding.UTF8);
            }
            else if (link.EndsWith(".svg")) //ToDo: SVG Implementieren
            {   //statische SVG-Datei ausliefern
                //var fileBytes = File.ReadAllBytesAsync(link);
                var file = File.ReadAllText(link, Encoding.UTF8);
                return Results.Content(file, "image/svg+xml", Encoding.UTF8);
   
            }
            else if (link.EndsWith(".json"))
            {
                string html = SollPageBuilder.BuildSollPageFromJsonFile(link).Result;
                //dynamisches Erstellen der Sollwert-Seite aus JSON-Datei
                return Results.Content(html, "text/html", Encoding.UTF8);                
            }

            return Results.Content($"<h1>Ungültiger Link: {link}</h1>", "text/html", Encoding.UTF8);

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
            sb.AppendLine("<p>Es wird ein Software-Neustart versucht.<br/>" +
                "Das Modul sollte innerhalb weniger Augenblicke wieder erreichbar sein.<br/>" +
                "</p>");
            //sb.Append("<p>Sollte das Modul , kann das Modul neugestartet werden, indem die Spannungsversorgung für 10 Sekunden unterbrochen wird.</p>");
            sb.Append(@"
                </body>
                </html>");

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html";
            await ctx.Response.WriteAsync(sb.ToString());
            await ctx.Response.CompleteAsync();

            PleaseStop = true;

            RestartServer();
        }

        private static void RestartServer()
        {
            PleaseStop = true;

            if (Environment.OSVersion.Platform == PlatformID.Unix) //nur Linux
            {
                new Thread(() =>
                {
                    //Warte 2 Sekunden, damit die Antwort an den Client gesendet werden kann
                    Thread.Sleep(2000);
                    var psi = new ProcessStartInfo("sudo")
                    {
                        Arguments = "systemctl restart kreuwebapp.service" //Dienstname anpassen / automatisch auslesbar?
                    };
                    Process.Start(psi);
                }).Start();
            }
        }

        private static IResult MainMenu()
        {
            //ctx.Response.StatusCode = 200;
            //ctx.Response.ContentType = "text/html";
            //var file = File.ReadAllText("wwwroot/html/menu.html", Encoding.UTF8);
            //await ctx.Response.WriteAsync(file);
            //await ctx.Response.CompleteAsync();

            var file = File.ReadAllText("wwwroot/html/menu.html");
            return Results.Content(file, "text/html");
        }

        //nicht implementiert
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
