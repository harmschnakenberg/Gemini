using Gemini.Db;
using Gemini.Models;
using Gemini.Services;
using S7.Net;
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
            sb.Append("<div class='container controls' style='width:600px;'>");
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

            sb.Append("</table></div><hr/>");


            sb.Append("<h2>Benutzer verwalten</h2>");
            sb.Append("<div class='container controls' style='width:600px;'>");
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

            sb.Append("</table>");
            sb.Append("</div><div class='container controls' style='width:600px;'>");

            if (isAdmin) 
                sb.Append("<button class='myButton' onclick='updateUser(\"create\")'>neu anlegen</button>");
            if (isAdmin || isUser) 
                sb.Append("<button class='myButton' onclick='updateUser(\"update\")'>ändern</button>");
            if (isAdmin) 
                sb.Append("<button class='delete-btn' onclick='updateUser(\"delete\")'>löschen</button>");

            sb.Append(@"</div>
            <script>
                function getUserData(row)
                {
                    const username = row.children[0].children[0].value;
                    const userrole = row.children[1].children[0].value;
                    document.getElementById('username').value = username;
                    document.getElementById('role').value = userrole;
                }

                async function updateUser(verb)
                {
                    const username = document.getElementById('username').value;
                    const userrole = document.getElementById('role').value;
                    const userpwd = document.getElementById('pwd').value;
                   
                    try
                    {
                        const res = await fetchSecure('/user/' + verb, {
                          method: 'POST', 
                          headers: { 'Content-Type': 'application/x-www-form-urlencoded' }, 
                          body: new URLSearchParams({ name: username, role: userrole, pwd: userpwd })
                        });

                        if (res.ok) {
                            location.reload();
                        } else {
                            alert('Benuterverwaltung - Nicht erlaubte Operation - Status ' + res.status);
                        }
                    } catch (error) {
                        console.error(error.message);
                    }
                }
            </script>");

            sb.Append("</body></html>");
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


        internal static async Task<string> ListAllPlcConfigs(bool isReadonly)
        {
           List<PlcConf> allPlcs = Db.Db.SelectAllPlcs();

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

            sb.Append("<h1>Datenquellen</h1>");
            sb.Append("<table>");
            sb.Append("<tr><th>Name</th><th>Type</th><th>IP</th><th>Rack</th><th>Slot</th><th>Aktiv</th><th>Bemerkung</th></tr>");

            foreach (PlcConf plc in allPlcs)
            {
                sb.Append("<tr>");
                sb.Append($"<td><input onchange='updPlc(\"update\", this);' value='{plc.Name}' {(isReadonly ? "disabled": string.Empty)}></td>");                
                sb.Append($"<td><select onchange='updPlc(\"update\", this);' {(isReadonly ? "disabled": string.Empty)}>");

                foreach (string type in Enum.GetNames<CpuType>())                                    
                    sb.Append($"<option value='{type}' {(type == plc.CpuType.ToString() ? "selected" : string.Empty)}>{type}</option>");
                
                sb.Append("</select></td>");
                sb.Append($"<td><input onchange='updPlc(\"update\", this);' pattern='[0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+' value='{plc.Ip}' {(isReadonly ? "disabled" : string.Empty)}></td>");
                sb.Append($"<td><input onchange='updPlc(\"update\", this);' type='number' min='0' size='2' value='{plc.Rack}'  {(isReadonly ? "disabled" : string.Empty)} ></td>");
                sb.Append($"<td><input onchange='updPlc(\"update\", this);' type='number' min='0' size='2' value='{plc.Slot}'  {(isReadonly ? "disabled" : string.Empty)} ></td>");         
                sb.Append($"<td><input onchange='updPlc(\"update\", this); 'type='checkbox' value='{plc.IsActive}'{(plc.IsActive == true ? "checked" : "")} {(isReadonly ? "disabled" : string.Empty)}></td>");
                sb.Append($"<td><input onchange='updPlc(\"update\", this);' value='{plc.Comment}' {(isReadonly ? "disabled" : string.Empty)}></td>");
                sb.Append($"<td><input type='hidden' value='{plc.Id}'></td>");
                
                if (!isReadonly)
                {
                    sb.Append("<td><input onclick='updPlc(\"ping\", this);' type='button' value='Ping'></td>");
                    sb.Append("<td><input onclick='updPlc(\"create\", this);' type='button' value='Duplizieren'></td>");
                    if (allPlcs.Count > 1)
                        sb.Append($"<td><input onclick='updPlc(\"delete\", this);' type='button' class='delete-btn' value='Löschen' {(allPlcs.Count > 1 ? string.Empty : "disabled")}></td>");
                }
                sb.Append("</tr>");
            }

            sb.Append("</table>");

            if (!isReadonly)
                sb.Append(@"
                <script>
                    
                async function updPlc(verb, obj) {
                    const plcName = obj.parentNode.parentNode.children[0].children[0].value;
                    const plcType = obj.parentNode.parentNode.children[1].children[0].value;
                    const plcIp = obj.parentNode.parentNode.children[2].children[0].value;
                    const plcRack = obj.parentNode.parentNode.children[3].children[0].value;
                    const plcSlot = obj.parentNode.parentNode.children[4].children[0].value;
                    const plcIsActive = obj.parentNode.parentNode.children[5].children[0].checked;
                    const plcComm = obj.parentNode.parentNode.children[6].children[0].value;
                    const plcId = obj.parentNode.parentNode.children[7].children[0].value;

                    //document.getElementsByTagName('h1')[0].innerHTML = `Id ${plcId}, ${plcName}, ${plcType}, ${plcIp}, ${plcRack}, ${plcSlot}, ${plcIsActive}, ${plcComm}|`;
                    try {
                        const res = await fetchSecure('/source/' + verb, {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                            body: new URLSearchParams({
                                plcId: plcId,
                                plcName: plcName,
                                plcType: plcType,
                                plcIp: plcIp,
                                plcRack: plcRack,
                                plcSlot: plcSlot,
                                plcIsActive: plcIsActive,
                                plcComm: plcComm
                            })
                        });

                        if (!res.ok)
                            alertError('Datenquellenverwaltung - Nicht erlaubte Operation - Status ' + res.status);
                        else {
                            const data = await res.json();
       console.log(data.type + ' ' + data.text);
                            if (data.type == 'reload') {
                                alertSuccess(`Operation ${verb} erfolgreich. ${data.text}`);
                                setTimeout(location.reload(), 5000);                
                            }
                            else
                                message(data.type, data.text);
                        }  

                    } catch (error) {
                        console.error(""Fehler beim Abrufen: "", error);
                    }
  
                }
       
                </script>
                ");

            sb.Append("<h2>Steuerungen</h2>");
            sb.Append("<p>Konfigurierte SPS-Steuerungen</p>");
            sb.Append("<table>");

            var plcs = PlcTagManager.Instance.GetAllPlcs();

            foreach (var plcName in plcs.Keys)
                if (plcName.StartsWith('A'))
                {
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<td>{plcName}</td>");
                    sb.AppendLine($"<td>{plcs[plcName].CPU.ToString()}<td/>");
                    sb.AppendLine($"<td>{plcs[plcName].IP}<td/>");
                    sb.AppendLine($"<td>Rack {plcs[plcName].Rack}<td/>");
                    sb.AppendLine($"<td>Slot {plcs[plcName].Slot}</td>");
                    sb.AppendLine($"<td>" +
                        $"<input type='number' style='width:2rem;' data-name='{plcName}_DB10_DBW2' readonly />:" +
                        $"<input type='number' style='width:2rem;' data-name='{plcName}_DB10_DBW4' readonly />:" +
                        $"<input type='number' style='width:2rem;' data-name='{plcName}_DB10_DBW6' readonly />" +
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
