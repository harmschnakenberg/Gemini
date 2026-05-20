using Gemini.Models;
using System.Diagnostics;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Gemini.Middleware
{
    /// <summary>
    /// Endpunkte, die HTML-Seiten oder andere statische Dateien ausliefern, sowie der Server-Stop-Endpunkt.
    /// </summary>
    public static partial class Endpoints
    {
        private static readonly string ServiceName = "kreuwebapp.service";
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
            {
                var encodedMessage = HtmlEncoder.Default.Encode(entry.Item3);
                var encodedLevel = HtmlEncoder.Default.Encode(entry.Item2);
                sb.AppendLine($"<tr><td style='width:20rem;'>{entry.Item1.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td><td>{encodedLevel}</td><td>{encodedMessage}</td></tr>");
            }

            sb.AppendLine("</tbody>");
            sb.AppendLine("</table>");

            sb.Append(@"
                </body>
                </html>");

            return Results.Content(sb.ToString(), "text/html", Encoding.UTF8);
        }

        private static IResult ShowSoll(int id, HttpContext ctx) { return ShowPageOrMenu(id, ctx, "soll"); }

        private static IResult ShowBild(int id, HttpContext ctx) { return ShowPageOrMenu(id, ctx, "bild"); }


        private static string? ValidateFilePath(string basePath, string relativePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(relativePath))
                    throw new InvalidOperationException("Path cannot be empty");

                var fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath));
                var baseFull = Path.GetFullPath(basePath);

                // Sicherstellen, dass fullPath innerhalb von basePath liegt
                if (!fullPath.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase))
                {
                    Db.Db.DbLogWarn($"Path traversal attempt detected: {relativePath}");
                    throw new InvalidOperationException("Path traversal attempt detected");
                }

                // Nur HTML, SVG, JSON erlauben
                var ext = Path.GetExtension(fullPath).ToLowerInvariant();
                if (!new[] { ".html", ".svg", ".json" }.Contains(ext))
                {
                    Db.Db.DbLogWarn($"Invalid file type: {ext} for path {relativePath}");
                    throw new InvalidOperationException($"File type {ext} not allowed");
                }

                if (!File.Exists(fullPath))
                {
                    Db.Db.DbLogWarn($"Requested file does not exist: {fullPath}");
                    return null; // Ok, einfach nicht vorhanden
                }

                return fullPath;
            }
            catch (InvalidOperationException ex)
            {
                Db.Db.DbLogError($"Path validation failed: {ex.Message}");
                return null;
            }
            catch (Exception ex) // Andere Exceptions (z.B. IO-Fehler)
            {
                Db.Db.DbLogError($"Unexpected error during path validation: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lese Dateien nur bis zu einer bestimmten größe, um DoS-Angriffe zu verhindern. Logge Fehler und Rückgabe null bei Problemen.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="maxSize">Max. Größe der Datei in Byte</param>
        /// <returns></returns>
        private static string? SafeReadFile(string filePath, long maxSize = 1048576)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > maxSize)
                {
                    Db.Db.DbLogError($"Datei zu groß: {filePath} ({fileInfo.Length} bytes, max {maxSize / 1024} KB)");
                    return null;
                }

                return File.ReadAllText(filePath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Db.Db.DbLogError($"Error reading file {filePath}: {ex.Message}");
                return null;
            }
        }

        private static IResult ShowPageOrMenu(int id, HttpContext ctx, string subfolder)
        {
            // Validierung Subfolder
            if (!new[] { "soll", "bild" }.Contains(subfolder?.ToLowerInvariant() ?? ""))
                return Results.BadRequest();

            string? json = SafeReadFile($"wwwroot/html/{subfolder}/menu.json");
            if (json == null)
                return Results.BadRequest("Datei konnte nicht geladen werden");

            //using (TextReader reader = new StreamReader($"wwwroot/html/{subfolder}/menu.json"))
            //{
            //    json = reader.ReadToEndAsync().Result;
            //};

            string link = "wwwroot/html/menu.html";

            Dictionary<string, MenuLink[]>? menuTree = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.DictionaryStringMenuLinkArray);

            if (menuTree != null)
            {
                MenuLink? menuLink = menuTree.First().Value?.Where(i => i.Id == id).FirstOrDefault();
                if (id != 0 && !string.IsNullOrEmpty(menuLink?.Link))
                {
                    string? validatedPath = ValidateFilePath($"wwwroot/html/{subfolder}", menuLink.Link);
                    if (validatedPath == null)
                        return Results.BadRequest("Ungültiger Pfad");

                    link = validatedPath;
                }
            }

            const long MaxFileSize = 5 * 1024 * 1024; // 5 MB für HTML      
            bool canEdit = ctx.User.IsInRole(Db.Role.User.ToString()) || ctx.User.IsInRole(Db.Role.Admin.ToString());

            if (link.EndsWith(".html"))
            {   //statische HTML-Datei ausliefern   
                string? file = SafeReadFile(link, MaxFileSize);
                if (file == null)
                    return Results.BadRequest("Datei konnte nicht geladen werden");

                if (!canEdit)
                {   // Zugriff auf Input-Felder verweigern, wenn der Benutzer keine Bearbeitungsrechte hat
                    file = file.Replace("<input", "<input readonly")
                        .Replace("class=\"checkbox\"", "class=\"checkbox readonly\"")             
                        .Replace("<select", "<select readonly");
                }

                return Results.Content(file, "text/html", Encoding.UTF8);
            }
            else if (link.EndsWith(".svg"))
            {   //statische SVG-Datei ausliefern
                string? file = SafeReadFile(link, MaxFileSize);
                if (file == null)
                    return Results.BadRequest("Datei konnte nicht geladen werden");
                
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

            await RestartServerAsync();
        }

        private static async Task RestartServerAsync()
        {
            PleaseStop = true;

            if (Environment.OSVersion.Platform == PlatformID.Unix) //nur Linux
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000); // Besser als Thread.Sleep

                    var psi = new ProcessStartInfo
                    {
                        FileName = "/bin/systemctl", // Absolute Pfad, nicht "sudo"
                        Arguments = $"restart {ServiceName}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    try
                    {
                        using var process = Process.Start(psi);
                        if (process != null)
                        {
                            await process.WaitForExitAsync();
                            Db.Db.DbLogInfo($"Systemd restart completed with exit code: {process.ExitCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Db.Db.DbLogError($"Failed to restart service: {ex.Message}");
                    }
                });
            }
        }

        private static IResult MainMenu()
        {
            string? version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();            
            var file = SafeReadFile("wwwroot/html/menu.html");
            file += $"<footer><p>Version: {version}</p></footer>";
            return Results.Content(file, "text/html");
        }

        //nicht implementiert
        private class SollPageBuilder
        {
            /// <summary>
            /// TODO: Implement this method
            /// Asynchronously builds a SOLL page by processing the JSON file located at the specified link.
            /// </summary>
            /// <param name="linkToJson">The URL or file path to the JSON file containing the data required to construct the SOLL page. Cannot be
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


                string? json = SafeReadFile(linkToJson, 1024 * 1024); // 1 MB
                if (json == null)                
                    return sb.ToString() + "<h1>Ungültiger Pfad zur Vorlagedatei</h1></body></html>";
                
                //    using (TextReader reader = new StreamReader(linkToJson))
                //{
                //    json = await reader.ReadToEndAsync();
                //}
                //;
#if DEBUG
                Console.WriteLine(json);
#endif
                SollwertFromJson[]? sollList = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.SollwertFromJsonArray);

                if (sollList == null)
                {
#if DEBUG
                    Console.WriteLine("BuildSollPageFromJsonFile() Konnte nicht geparsed werden: " + json);
#endif
                    return sb.ToString() + "<h1>Ungültige Vorlagedatei</h1></body></html>";
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
