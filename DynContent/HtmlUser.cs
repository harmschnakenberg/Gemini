using Gemini.Db;
using Gemini.Services;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace Gemini.DynContent
{
    public static partial class HtmlHelper
    {
        /// <summary>
        /// Erzeugt ein HTML-`<option>`-Element für eine Rollen-Auswahl. Fügt das Attribut "selected" hinzu,
        /// wenn die übergebene aktuelle Rolle mit der angegebenen Rollen-Option übereinstimmt.
        /// </summary>
        /// <param name="role">Die aktuell zugewiesene Rolle des Benutzers (Vergleichsbasis).</param>
        /// <param name="roleOption">Die darzustellende Rollen-Option (wird als Wert des `value`-Attributs verwendet).</param>
        /// <param name="roleName">Die für den Benutzer sichtbare Bezeichnung der Rolle.</param>
        /// <returns>Ein `string`, der das vollständige `<option>`-HTML-Element enthält; ggf. mit dem Attribut `selected`.</returns>
        private static string RoleOption(Role role, Role roleOption, string roleName)
        {
            return $"<option value='{roleOption}' {(role == roleOption ? "selected" : string.Empty)}>{roleName}</option>";
        }

        /// <summary>
        /// Generates an HTML representation of all users and their roles, allowing for user management actions based on
        /// the current user's role.
        /// </summary>
        /// <remarks>The method restricts visibility of user information based on the current user's role;
        /// only admins can see all users, while regular users can only see their own information.</remarks>
        /// <param name="users">A list of User objects representing the users to be displayed in the HTML output.</param>
        /// <param name="currentUser">The ClaimsPrincipal representing the currently authenticated user, used to determine the user's role and
        /// permissions.</param>
        /// <returns>A string containing the generated HTML markup for displaying the user list and management options.</returns>
        internal static string ListAllUsers(List<User> users, ClaimsPrincipal currentUser)
        {
            var username = currentUser.Identity?.Name ?? "Unbekannt";
            string role = currentUser.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value.ToLower() ?? "Unbekannt";
            int currentUserId = 0;
            bool isAdmin = role.Equals("admin");
            bool isUser = role.Equals("user");
            bool isGuest = role.Equals("guest");

            StringBuilder sb = new();

            sb.AppendLine(@"<!DOCTYPE html>
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

            sb.AppendLine("<h1>Benutzerverwaltung</h1>");

            if (isAdmin)
            {
                sb.AppendLine("<div class='container controls' style='width:600px;'>");
                sb.AppendLine("<table><tr><th>Benutzer</th><th>Rolle</th></tr>");

                foreach (var u in users)
                {
                    if (!isAdmin && u.Name != username) //Benutzer können nur sich selbst sehen.
                        continue;

                    if (u.Name == username)
                        currentUserId = u.Id;

                    sb.AppendLine("<tr onclick='user.getUserDataFromTableRow(this);'>");
                    sb.AppendLine($"<td><input class='myInput' value='{u.Name}' readonly></td>");
                    sb.AppendLine("<td><select class='myInput' disabled>");
                    sb.AppendLine(RoleOption(u.Role, Role.Guest, "Gast"));
                    sb.AppendLine(RoleOption(u.Role, Role.User, "Benutzer"));
                    sb.AppendLine(RoleOption(u.Role, Role.Admin, "Administrator"));
                    sb.AppendLine("</select></td>");
                    sb.AppendLine($"<td><input value='{u.Id}' readonly style='display:none;'></td>");
                    sb.AppendLine("</tr>");
                }

                sb.AppendLine("</table></div><hr/>");
                sb.AppendLine("<h2>Benutzer ändern</h2>");
            }

            
            sb.AppendLine("<div class='container controls' style='width:600px;'>");
            sb.AppendLine("<table><tr><th>Benutzer</th><th>Rolle</th><th>Passwort</th></tr>");
            sb.AppendLine("<tr>");

            sb.AppendLine($"<td><input class='myInput' id='username' placeholder='neuer Benutzername' required {(isUser||isGuest ? $"value='{username}' readonly" : string.Empty)}></td>");
            sb.AppendLine($"<td><select class='myInput' id='role' {(!isAdmin ? "readonly" : string.Empty)}>");

            if (isAdmin || isGuest)
                sb.AppendLine(RoleOption(0, Role.Guest, "Gast"));

            if (isAdmin || isUser)
                sb.AppendLine(RoleOption(0, Role.User, "Benutzer"));

            if (isAdmin) //nur Admins können Admins auswählen
                sb.AppendLine(RoleOption(0, Role.Admin, "Administrator"));

            sb.AppendLine("</select></td>");
            sb.AppendLine($"<td><input class='myInput' id='pwd' type='password' placeholder='********'>");
            sb.AppendLine($"<td><input id='userid' value='{currentUserId}' style='display:none;'></td>");
            sb.AppendLine("</tr>");

            sb.AppendLine("</table>");
            sb.AppendLine("</div><div class='container controls' style='width:600px;'>");

            if (isAdmin)
                sb.AppendLine("<button class='myButton' onclick='user.updateUser(\"create\")'>neu anlegen</button>");
            if (isAdmin || isUser || isGuest)
                sb.AppendLine("<button class='myButton' onclick='user.updateUser(\"update\")'>ändern</button>");
            if (isAdmin)
                sb.AppendLine("<button class='delete-btn' onclick='user.updateUser(\"delete\")'>löschen</button>");
            
            sb.AppendLine("</div>");

            if (isAdmin || isUser)
            {
                List<IPAddress> cInfo = [.. PlcTagManager.Instance.ClientInfo.Values];

                sb.AppendLine($"<div style='position:fixed; bottom:0; right:0; opacity:0.7;font-size:0.8rem;'><b>angemeldet:</b>");
                foreach (var ci in cInfo)
                    sb.AppendLine($"<div>{ci}</div>");

                sb.AppendLine("</div>");
            }

            sb.AppendLine(@"
              </body>
            </html>");
            return sb.ToString();
        }

    }
}
