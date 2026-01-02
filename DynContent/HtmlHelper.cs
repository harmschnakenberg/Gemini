using Gemini.Db;
using Gemini.Services;
using Microsoft.Extensions.Primitives;
using System.Data;
using System.Text;

namespace Gemini.DynContent
{
    public class HtmlHelper
    {
      
        internal static string ListAllUsers(List<User> users)
        {
            StringBuilder sb = new();

            sb.Append(@"<!DOCTYPE html>
                            <html lang='de'>
                            <head>
                                <meta charset='UTF-8'>
                                <title>Alle Werte</title>                    
                                <link rel='shortcut icon' href='/favicon.ico'>
                                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                                <link rel='stylesheet' href='/css/style.css'>                    
                                <script src='/js/websocket.js'></script>
                                <script src='/js/excel.js'></script>
                            </head>
                            <body>");

            sb.Append($"<h1>Benutzerübersicht</h1>");
            sb.Append("<table><tr><th>Benutzer</th><th>Rolle</th></tr>");

            foreach (var u in users)
            {
                sb.Append("<tr>");
                sb.Append($"<td><input value='{u.Name}' disabled></td>");
                sb.Append("<td><select>");
                sb.Append(RoleOption(u.Role, "user", "Benutzer"));
                sb.Append(RoleOption(u.Role, "admin", "Administrator"));
                sb.Append("</select></td>");
                sb.Append("</tr>");
            }

            sb.Append("<tr>");
            sb.Append($"<td><input id='username' placeholder='neuer Benutzername' required></td>");
            sb.Append("<td><select id='role'>");
            sb.Append(RoleOption(string.Empty, "user", "Benutzer"));
            sb.Append(RoleOption(string.Empty, "admin", "Administrator"));
            sb.Append("</select></td>");
            sb.Append($"<td><input id='pwd' type='password' placeholder='********'><label for='pwd'>Passwort</label></td>");
            sb.Append("</tr>");

            sb.Append("</table></body></html>");

            sb.Append("<button onclick='createUser()'>neu anlegen</button>");

            sb.Append(@"
            <script>
                function createUser() {                    
                    const username = document.getElementById('username').value;
                    const userrole = document.getElementById('role').value;
                    const userpwd = document.getElementById('pwd').value;

                    post('/user/create', { name: username, role: userrole, pwd: userpwd });
                }
            </script>");

            return sb.ToString();
        }


        private static string RoleOption(string role, string roleOption, string roleName)
        {
            return $"<option value='{roleOption}' {(role.Equals(roleOption, StringComparison.CurrentCultureIgnoreCase) ? "selected" : string.Empty)}>{roleName}</option>";
        }

        internal static async Task<string> ListAllTags() {             
            
            StringBuilder sb = new();

            sb.Append(@"<!DOCTYPE html>
                <html lang='de'>
                <head>
                    <meta charset='UTF-8'>
                    <title>Alle Werte</title>                    
                    <link rel='shortcut icon' href='/favicon.ico'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <link rel='stylesheet' href='/css/style.css'>                    
                    <script src='/js/websocket.js'></script>
                    <script src='/js/excel.js'></script>
                </head>
                <body>");


            List<Models.Tag> allTags = await Db.Db.GetDbTagNames(DateTime.UtcNow, 1);


            sb.Append("<h1>Datenpunkte</h1>");
            sb.Append("<table>");
            sb.Append("<tr><th>Item-Name</th><th>Beschreibung</th><th>mom. Wert</th><th>Log</th></tr>");

            foreach (Models.Tag tag in allTags)
            {
                sb.Append("<tr>");
                sb.Append($"<td><input value='{tag.TagName}' disabled></td>");
                sb.Append($"<td><input style='text-align: left;' onchange='updateTag(this);' value='{tag.TagComment}'></td>");
                sb.Append($"<td><input data-name='{tag.TagName}' disabled></td>");
                sb.Append($"<td><input type='checkbox' onchange='updateTag(this);' value='{tag.ChartFlag}'{(tag.ChartFlag == true ? "checked" : "")} ></td>");
                sb.Append("</tr>");
            }

            sb.Append("</table>");

            sb.Append(@"
            <script>
                function updateTag(obj) {                    
                    const tagName = obj.parentNode.parentNode.children[0].children[0].value;
                    const tagComm = obj.parentNode.parentNode.children[1].children[0].value;
                    const tagChck = obj.parentNode.parentNode.children[3].children[0].checked;

                   // post('/tagupdate', { tagName: tagName, tagComm: tagComm, tagChck: tagChck });
                    fetch('/tagupdate', {
                      method: 'POST',
                      credentials: 'include', // wichtig für Cookie
                      headers: {
                        'Content-Type': 'application/x-www-form-urlencoded',
                        'X-CSRF-TOKEN-HEADERNAME': 'KreuAntiforgeryCookie'
                      },
                      body: new URLSearchParams({ tagName: tagName, tagComm: tagComm, tagChck: tagChck })
                    });
                }
            </script>
            ");

            sb.Append("<h2>Steuerungen</h2>");
            sb.Append("<p>Konfigurierte SPS-Steuerungen</p>");
            sb.Append("<table>");

            var plcs = PlcTagManager.Instance.GetAllPlcs();

            foreach (var plcName in plcs.Keys)
                if (plcName.StartsWith('A')) {
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td>{plcName}</td>");
                    sb.AppendLine($"<td>{plcs[plcName].CPU.ToString()}<td/>");
                    sb.AppendLine($"<td>{plcs[plcName].IP}<td/>");
                    sb.AppendLine($"<td>Rack {plcs[plcName].Rack}<td/>");
                    sb.AppendLine($"<td>Slot {plcs[plcName].Slot}</td>");
                    sb.AppendLine($"<td>" +
                        $"<input type='number' style='width:2rem;' data-name='{plcName}_DB10_DBW2'/>:" +
                        $"<input type='number' style='width:2rem;' data-name='{plcName}_DB10_DBW4'/>:" +
                        $"<input type='number' style='width:2rem;' data-name='{plcName}_DB10_DBW6'/>" +
                        $"</td>");                   
                    sb.AppendLine("</tr>");
                }

            sb.Append("</table>");

            sb.Append(@"
                </body>
                </html>");

            return sb.ToString();
        }

        internal static string ExitForm()
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

            return sb.ToString();
        }
    }
}
