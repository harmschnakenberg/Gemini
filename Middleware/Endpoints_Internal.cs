using Gemini.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Gemini.Middleware
{
    public static partial class Endpoints
    {

        private static IResult ShowLog(HttpContext context)
        {
            List<Tuple<DateTime, string, string>> logEntries = Db.Db.GetLogEntries(1000);

            StringBuilder sb = new();

            sb.Append(@"<!DOCTYPE html>
                <html lang='de'>
                <head>
                    <meta charset='UTF-8'>
                    <title>Server Log</title>
                    <link rel='icon' type='image/x-icon' href='/favicon.ico'>                  
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <link rel='stylesheet' href='../css/style.css'>    
                    <script type='module' src='../js/script.js'></script>
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

        private static IResult ShowSoll(int id, HttpContext ctx) { return ShowPageOrMenu(id, ctx, "soll"); }

        private static IResult ShowBild(int id, HttpContext ctx) { return ShowPageOrMenu(id, ctx, "bild"); }

        private static IResult ShowPageOrMenu(int id, HttpContext ctx, string subfolder)
        {
            string json;
            using (TextReader reader = new StreamReader($"wwwroot/html/{subfolder}/menu.json"))
            {
                json = reader.ReadToEndAsync().Result;
            }
            ;

            //Console.WriteLine(json);
            string link = "wwwroot/html/menu.html";

            Dictionary<string, MenuLink[]>? menuTree = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.DictionaryStringMenuLinkArray);

            if (menuTree != null)
            {
                MenuLink? menuLink = menuTree.First().Value?.Where(i => i.Id == id).FirstOrDefault();
                if (id != 0 && !string.IsNullOrEmpty(menuLink?.Link))
                    link = $"wwwroot/html/{subfolder}/{menuLink?.Link ?? string.Empty}";
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
                    <script src='/js/menu.js'></script>
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
                    <script src='../module/fetch.js'></script>
                </head>
                <body>");


                string json;
                using (TextReader reader = new StreamReader(linkToJson))
                {
                    json = await reader.ReadToEndAsync();
                }
                ;
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
