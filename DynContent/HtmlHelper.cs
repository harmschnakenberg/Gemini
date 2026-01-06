using Gemini.Db;
using Gemini.Services;
using System.Data;
using System.Security.Claims;
using System.Text;

namespace Gemini.DynContent
{
    public class HtmlHelper
    {

        internal static string ListAllUsers(List<User> users, ClaimsPrincipal currentUser)
        {
            var username = currentUser.Identity?.Name ?? "Unbekannt";
            string role = currentUser.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value.ToLower() ?? "Unbekannt";
            //Console.WriteLine($"ListAllUsers()) als {role}");

            bool isAdmin = role.Equals("admin");
            bool isUser = role.Equals("user");


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

            sb.Append("<h1>Benutzerübersicht</h1>");
            //sb.Append($"<div>{role}</div>");
            sb.Append("<table><tr><th>Benutzer</th><th>Rolle</th></tr>");

            if(isAdmin || isUser)
                foreach (var u in users)
                {
                    if (isUser && u.Name != username) //Benutzer können nur sich selbst sehen.
                        continue;

                    sb.Append("<tr onclick='getUserData(this);'>");
                    sb.Append($"<td><input value='{u.Name}' readonly></td>");
                    sb.Append("<td><select disabled>");
                    sb.Append(RoleOption(u.Role, Role.User, "Benutzer"));
                    sb.Append(RoleOption(u.Role, Role.Admin, "Administrator"));
                    sb.Append("</select></td>");
                    sb.Append("</tr>");
                }

            sb.Append("</table><hr/>");


            sb.Append("<h2>Benutzer verwalten</h2>");
            sb.Append("<table><tr><th>Benutzer</th><th>Rolle</th><th>Passwort</th></tr>");
            sb.Append("<tr>");
            sb.Append($"<td><input id='username' placeholder='neuer Benutzername' required {(isUser ? $"value='{username}' readonly" : string.Empty)}></td>");
            sb.Append("<td><select id='role'>");
            sb.Append(RoleOption(0, Role.User, "Benutzer"));
            
            if (isAdmin) //nur Admins können Admins auswählen
                sb.Append(RoleOption(0, Role.Admin, "Administrator"));
            
            sb.Append("</select></td>");
            sb.Append($"<td><input id='pwd' type='password' placeholder='********'>");
            sb.Append("</tr>");

            sb.Append("</table></body></html>");

            if (isAdmin) 
                sb.Append("<button class='myButton' onclick='updateUser(\"create\")'>neu anlegen</button>");
            if (isAdmin || isUser) 
                sb.Append("<button class='myButton' onclick='updateUser(\"update\")'>ändern</button>");
            if (isAdmin) 
                sb.Append("<button class='myButton' onclick='updateUser(\"delete\")'>löschen</button>");

            sb.Append(@"
            <script>
                function getUserData(row)
                {
                    const username = row.children[0].children[0].value;
                    const userrole = row.children[1].children[0].value;
                    document.getElementById('username').value = username;
                    document.getElementById('role').value = userrole;
                }

                function updateUser(verb)
                {
                    const username = document.getElementById('username').value;
                    const userrole = document.getElementById('role').value;
                    const userpwd = document.getElementById('pwd').value;
                   
                    fetchSecure('/user/' + verb, {
                          method: 'POST', 
                          headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, 
                          body: new URLSearchParams({ name: username, role: userrole, pwd: userpwd })
                        });
                }
            </script>");
      
            return sb.ToString();
        }


        private static string RoleOption(Role role, Role roleOption, string roleName)
        {
            return $"<option value='{roleOption}' {(role == roleOption ? "selected" : string.Empty)}>{roleName}</option>";
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

                    fetchSecure('/tagupdate', {
                      method: 'POST',   
                      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },                      
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
