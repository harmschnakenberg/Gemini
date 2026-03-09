using Gemini.Db;
using Gemini.Services;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace Gemini.DynContent
{
    public static partial class HtmlHelper
    {

        private static string RoleOption(Role role, Role roleOption, string roleName)
        {
            return $"<option value='{roleOption}' {(role == roleOption ? "selected" : string.Empty)}>{roleName}</option>";
        }

        internal static string ListAllUsers(List<User> users, ClaimsPrincipal currentUser)
        {
            var username = currentUser.Identity?.Name ?? "Unbekannt";
            string role = currentUser.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value.ToLower() ?? "Unbekannt";
            //Console.WriteLine($"ListAllUsers()) als {role}");

            bool isAdmin = role.Equals("admin");
            bool isUser = role.Equals("user");
            bool isGuest = role.Equals("guest");


            StringBuilder sb = new();

            sb.Append(@"<!DOCTYPE html>
                            <html lang='de'>
                            <head>
                                <meta charset='UTF-8'>
                                <title>Benutzerverwaltung</title>                    
                                <link rel='shortcut icon' href='/favicon.ico'>
                                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                                <link rel='stylesheet' href='../css/style.css'>     
                                <script type='module' src='../js/script.js'></script>
                            </head>
                            <body>");

            sb.Append("<h1>Benutzerübersicht</h1>");
            sb.Append("<div class='container controls' style='width:600px;'>");
            sb.Append("<table><tr><th>Benutzer</th><th>Rolle</th></tr>");

            if (isAdmin || isUser)
                foreach (var u in users)
                {
                    if (isUser && u.Name != username) //Benutzer können nur sich selbst sehen.
                        continue;

                    sb.Append("<tr onclick='user.getData(this);'>");                    
                    sb.Append($"<td><input value='{u.Name}' readonly></td>");
                    sb.Append("<td><select disabled>");
                    sb.Append(RoleOption(u.Role, Role.Guest, "Gast"));
                    sb.Append(RoleOption(u.Role, Role.User, "Benutzer"));
                    sb.Append(RoleOption(u.Role, Role.Admin, "Administrator"));
                    sb.Append("</select></td>");
                    sb.Append($"<td><input value='{u.Id}' readonly style='display:none;'></td>");
                    sb.Append("</tr>");
                }

            sb.Append("</table></div><hr/>");

            sb.Append("<h2>Benutzer verwalten</h2>");
            sb.Append("<div class='container controls' style='width:600px;'>");
            sb.Append("<table><tr><th>Benutzer</th><th>Rolle</th><th>Passwort</th></tr>");
            sb.Append("<tr>");
            
            sb.Append($"<td><input id='username' placeholder='neuer Benutzername' required {(isUser ? $"value='{username}' readonly" : string.Empty)}></td>");
            sb.Append("<td><select id='role'>");

            if (isAdmin || isGuest)
                sb.Append(RoleOption(0, Role.Guest, "Gast"));

            if (isAdmin || isUser)
                sb.Append(RoleOption(0, Role.User, "Benutzer"));

            if (isAdmin) //nur Admins können Admins auswählen
                sb.Append(RoleOption(0, Role.Admin, "Administrator"));

            sb.Append("</select></td>");
            sb.Append($"<td><input id='pwd' type='password' placeholder='********'>");
            sb.Append($"<td><input id='userid' value='0' style='display:none;'></td>");
            sb.Append("</tr>");

            sb.Append("</table>");
            sb.Append("</div><div class='container controls' style='width:600px;'>");

            if (isAdmin)
                sb.Append("<button class='myButton' onclick='user.update(\"create\")'>neu anlegen</button>");
            if (isAdmin || isUser)
                sb.Append("<button class='myButton' onclick='user.update(\"update\")'>ändern</button>");
            if (isAdmin)
                sb.Append("<button class='delete-btn' onclick='user.update(\"delete\")'>löschen</button>");

            sb.Append(@"</div>
            <script>
                //function getUserData(row)
                //{
                    
                //    const username = row.children[0].children[0].value;
                //    const userrole = row.children[1].children[0].value;
                //    const userid = row.children[2].children[0].value;

                //    document.getElementById('userid').value = userid;
                //    document.getElementById('username').value = username;
                //    document.getElementById('role').value = userrole;
                //}

                //async function updateUser(verb)
                //{
                //    const userid = document.getElementById('userid').value
                //    const username = document.getElementById('username').value;
                //    const userrole = document.getElementById('role').value;
                //    const userpwd = document.getElementById('pwd').value;
                   
                //    try
                //    {
                //        const ws = await import('../module/fetch.js');
                //        const res = await ws.fetchSecure('/user/' + verb, {
                //          method: 'POST', 
                //          headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, 
                //          body: new URLSearchParams({ id: userid, name: username, role: userrole, pwd: userpwd })
                //        });

                //        if (res.ok) {
                //            location.reload();
                //        } else {
                //            alert('Benuterverwaltung - Nicht erlaubte Operation - Status ' + res.status);
                //        }
                //    } catch (error) {
                //        console.error(error.message);
                //    }
                //}
            </script>");


            List<IPAddress> cInfo = [.. PlcTagManager.Instance.ClientInfo.Values];

            sb.Append($"<div style='position:fixed; bottom:0; right:0; opacity:0.5;font-size:0.8rem;'><b>angemeldet:</b>");
            foreach (var ci in cInfo)
            {
                sb.Append($"<div>{ci}</div>");
            }


            sb.AppendLine(@"</div>               
              </body>
            </html>");
            return sb.ToString();
        }

    }
}
