using Gemini.Models;
using System.Text;
using static Gemini.Db.Db;

namespace Gemini.DynContent
{
    public static partial class HtmlHelper
    {

        internal static string ListAllTags(bool isAdmin)
        {

            StringBuilder sb = new();

            sb.Append(@"<!DOCTYPE html>
                <html lang='de'>
                <head>
                    <meta charset='UTF-8'>
                    <title>Alle Werte</title>                    
                    <link rel='shortcut icon' href='/favicon.ico'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <link rel='stylesheet' href='../css/style.css'>      
                    <script type='module' src='../js/script.js'></script>
                </head>
                <body>");


            List<Models.Tag> allTags = GetDbTagNames(System.DateTime.UtcNow, 1);

            sb.Append("<h1>Datenpunkte</h1>");
            sb.AppendLine("<a href='/source' class='menuitem'>Datenquellen</a>");
            sb.AppendLine("<a href='/tag/failures' class='menuitem'>Letzte Lesefehler</a>");
            sb.AppendLine("<a href='/db/list' class='menuitem'>Datenbanken</a>");

            sb.Append("<h2>Gelesene Datenpunkte</h2>");
            sb.Append("<hr/><table>");
            sb.Append("<tr><th>Item-Name</th><th>Beschreibung</th><th>mom. Wert</th><th>Log</th></tr>");


            foreach (Models.Tag tag in allTags)
            {
                sb.Append("<tr>");
                sb.Append($"<td><input value='{tag.TagName}' disabled></td>");
                sb.Append($"<td><input style='text-align: left;' onchange='updateTag(this);' value='{tag.TagComment}' {(isAdmin ? "" : "disabled")}></td>");
                sb.Append($"<td><input data-name='{tag.TagName}' disabled></td>");
                sb.Append($"<td><input type='checkbox' onchange='updateTag(this);' value='{tag.ChartFlag}'{(tag.ChartFlag == true ? "checked" : "")}  {(isAdmin ? "" : "disabled")}></td>");
                sb.Append("</tr>");
            }

            sb.Append("</table>");

            sb.Append(@"
            <script>
                
                async function updateTag(obj) {                    
                    const tagName = obj.parentNode.parentNode.children[0].children[0].value;
                    const tagComm = obj.parentNode.parentNode.children[1].children[0].value;
                    const tagChck = obj.parentNode.parentNode.children[3].children[0].checked;

                    const ws = await import('../module/fetch.js');
                    ws.fetchSecure('/tag/update', {
                      method: 'POST',   
                      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },                      
                      body: new URLSearchParams({ tagName: tagName, tagComm: tagComm, tagChck: tagChck })
                    });
                }

            </script>
            ");

            sb.Append(@"
                </body>
                </html>");

            return sb.ToString();
        }


        /// <summary>
        /// Generates an HTML representation of altered user tags for display purposes.
        /// </summary>
        /// <remarks>The returned HTML is intended for use in a German-language user management interface.
        /// The structure includes a table summarizing tag changes by user and time.</remarks>
        /// <param name="aTags">A list of tuples containing tag alteration details. Each tuple includes the timestamp, user name, and
        /// associated tag information.</param>
        /// <returns>A string containing the generated HTML markup representing the altered tags, or null if no tags are
        /// provided.</returns>
        internal static string? ListAlteredTags(List<TagAltered> aTags, System.DateTime startUtc, System.DateTime endUtc)
        {
            //Console.WriteLine($"Gefundene Änderung: {time} | {user} | {tagName} | {tagComment} | {newValue} | {oldValue}");
            StringBuilder sb = new();

            sb.Append(@"<!DOCTYPE html>
                            <html lang='de'>
                            <head>
                                <meta charset='UTF-8'>
                                <title>Sollwertänderungen</title>                    
                                <link rel='shortcut icon' href='/favicon.ico'>
                                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                                <link rel='stylesheet' href='../css/style.css'>                    
                                <script type='module' src='../js/script.js'></script>
                            </head>
                            <body>");

            sb.Append($"<h1>{aTags.Count} Sollwertänderungen</h1>");

            sb.Append(@" <div class='container'>
                <label style='padding:0 1rem;' for='start'>Beginn</label>");
            sb.Append($"<input class='myButton' type='datetime-local' id='start' name='start' value='{startUtc.ToLocalTime().ToString("yyyy-MM-ddTHH:mm")}' onchange='getAlteredTags()'>");
            sb.Append("<label style='padding:0 1rem;' for='end'>Ende</label>");
            sb.Append($"<input class='myButton' type='datetime-local' id='end' name='end' value='{endUtc.ToLocalTime().ToString("yyyy-MM-ddTHH:mm")}' onchange='getAlteredTags()'>");
            //sb.Append("<button class='myButton' onclick='getAlteredTags()'>Filter anwenden</button>");
            sb.Append("</div>");

            sb.AppendLine("<table class='datatable'>");
            sb.AppendLine("<tr>" +
                "<th>Zeit</th>" +
                "<th>Benutzer</th>" +
                "<th>Bezeichnung</th>" +
                "<th>Wert neu</th>" +
                "<th>Wert alt</th>" +
                "</tr>");

            foreach (var tag in aTags)
            {
                //Zeit | User | TagName | TagComment | NewValue | OldValue
                sb.AppendLine("<tr>" +
                    $"<td>{tag.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}</td>" +
                    $"<td>{tag.User}</td>" +
                    $"<td style='display:none;'>{tag.TagName}</td>" +
                    $"<td>{tag.TagComment}</td>" +
                    $"<td>{tag.NewValue}</td>" +
                    $"<td>{tag.OldValue ?? "?"}</td>" +
                    "</tr>");
            }
            sb.Append("</table>");

            sb.AppendLine($"<p class='container'>" +
                        $"Änderungen von <strong>{startUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm")}</strong> " +
                        $"bis <strong>{endUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm")}</strong>" +
                        $"</p>");
            sb.Append($"<span style='position:sticky; left:0; bottom:0;'>Stand {System.DateTime.Now.ToShortTimeString()}</span>");

            sb.AppendLine(@"<script>

            async function getAlteredTags()
            {
                const start = document.getElementById('start').value;
                const end = document.getElementById('end').value;
               
                if ('URLSearchParams' in window) {
                    var searchParams = new URLSearchParams(window.location.search);
                    searchParams.set('start', start);
                    searchParams.set('end', end);
                    window.location.search = searchParams.toString();
                }

               const link = `/soll/history?start=${encodeURIComponent(start)}&end=${encodeURIComponent(end)}`;

               try {
                    const ws = await import('../module/fetch.js');
                    const response = await ws.fetchSecure(link);

                    if (!response.ok) 
                        throw new Error(`Response status: ${response.status}`);
                    
                    const html = await response.text();
                    document.body.innerHTML = html;
                } catch (error) {
                console.error('Fehler beim Laden:', error);
                }
                
            }

            function setDates() {
                const urlParams = new URLSearchParams(window.location.search);
                var s = urlParams.get('start');
                var e = urlParams.get('end');
      
                if(s)
                    document.getElementById('start').value = s;

                if(e)
                    document.getElementById('end').value = e;
            }

            setDates();
            </script>");


            sb.Append("</body></html>");
            return sb.ToString();
        }


        internal static string ListAllDatabases()
        {
            var dbFiles = Directory.GetFiles(Path.Combine(AppFolder, "db"), "*.db").Select(f => new FileInfo(f)).ToList().OrderByDescending(f => f.Name);

            StringBuilder sb = new();

            sb.Append(@"<!DOCTYPE html>
                <html lang='de'>
                <head>
                    <meta charset='UTF-8'>
                    <title>Datenbanken</title>                    
                    <link rel='shortcut icon' href='/favicon.ico'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <link rel='stylesheet' href='../css/style.css'>                    
                    <script type='module' src='../js/script.js'></script>
                </head>
                <body>");

            sb.Append("<h1>Datenbanken</h1>");
            sb.AppendLine("<a href='/tag/all' class='menuitem'>Gelesene Daten</a>");
            sb.AppendLine("<a href='/source' class='menuitem'>Datenquellen</a>");
            sb.AppendLine("<a href='/tag/failures' class='menuitem'>Letzte Lesefehler</a>");
            sb.Append($"<p class='controls'>{dbFiles.Count()} lokal gespeicherte Datenbanken.</p>");

            sb.Append("<table class='datatable'>");
            sb.Append("<tr><th>Dateiname</th><th>Größe</th><th>Letzte Änderung</th><th>Letzter Zugriff</th></tr>");
            foreach (var dbFile in dbFiles)
                sb.AppendLine($"<tr><td>{dbFile.Name}</td><td>{(dbFile.Length / 1024.0 / 1024.0).ToString("0.00")} MB</td><td>{(dbFile.LastWriteTime).ToString("yyyy-MM-dd HH:mm:ss")}</td><td>{(dbFile.LastAccessTime).ToString("yyyy-MM-dd HH:mm:ss")}</td>");

            sb.Append("</table>");

            sb.Append("<style>.datatable td {text-align: right;}</style>");

            sb.Append(@"
                </body>
                </html>");

            return sb.ToString();
        }

        internal static string TagReadFailures()
        {
            List<ReadFailure> readFailures = Db.Db.DbLogGetReadFailures();

            var sb = new StringBuilder(@"<!DOCTYPE html>
                <html lang='de'>
                <head>
                    <meta charset='UTF-8'>
                    <title>Alle Werte</title>                    
                    <link rel='shortcut icon' href='/favicon.ico'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <link rel='stylesheet' href='../css/style.css'>                    
                    <script type='module' src='../js/script.js'></script>
                </head>
                <body>");

            sb.Append("<h1>Lesefehler</h1>");
            sb.AppendLine("<a href='/tag/all' class='menuitem'>Gelesene Daten</a>");
            sb.AppendLine("<a href='/source' class='menuitem'>Datenquellen</a>");
            sb.AppendLine("<a href='/db/list' class='menuitem'>Datenbanken</a>");

            sb.Append("<p>Zuletzt aufgetretene Fehler beim Lesen aus SPS</p>");
            sb.Append("<table id='tagfailtable'>");
            sb.Append("<tr><th>IP</th><th>DB</th><th>Startbyte</th><th>Länge</th><th>Zeitpunkt</th></tr>");

            foreach (var fail in readFailures)
            {
                sb.Append($"<tr>");
                sb.Append($"<td>{fail.Ip}</td>");
                sb.Append($"<td>{fail.Db}</td>");
                sb.Append($"<td>{fail.StartByte}</td>");
                sb.Append($"<td>{fail.Length}</td>");
                sb.Append($"<td>{fail.Time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")}</td>");
                sb.Append($"</tr>");
            }

            sb.AppendLine($"<tr><td colspan='5'>Anzahl Lesefehler: {(readFailures.Count > 0 ? readFailures.Count : "keine Lesefehler")}</td></tr>");
            //sb.AppendLine($"<tr><td colspan='5'>Letzter Lesefehler: {(readFailures.Count > 0 ? readFailures.Max(rf => rf.Time).ToString("yyyy-MM-dd HH:mm:ss") : "keine Lesefehler")}</td></tr>");

            sb.Append("</table>");

            sb.Append(@"
                </body>
                </html>");

            return sb.ToString();
        }

    }
}
