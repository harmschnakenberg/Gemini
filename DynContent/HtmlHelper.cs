using Gemini.Db;
using Gemini.Services;
using S7.Net;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Text;
using static Gemini.Db.Db;

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
            bool isGuest = role.Equals("guest");


            StringBuilder sb = new();

            sb.Append(@"<!DOCTYPE html>
                            <html lang='de'>
                            <head>
                                <meta charset='UTF-8'>
                                <title>Benutzerverwaltung</title>                    
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
                    sb.Append(RoleOption(u.Role, Role.Guest, "Gast"));
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
            
            if (isAdmin || isGuest)
                sb.Append(RoleOption(0, Role.Guest, "Gast"));

            if (isAdmin || isUser)
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


            List<Models.Tag> allTags = GetDbTagNames(DateTime.UtcNow, 1);

            sb.Append("<h1>Datenpunkte</h1>");
            sb.AppendLine("<a href='/source' class='menuitem'>Datenquellen</a>");
            sb.AppendLine("<a href='/tag/failures' class='menuitem'>Letzte Lesefehler</a>");
            sb.Append("<h2>Gelesene Datenpunkte</h2>");
            sb.Append("<hr/><table>");
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

                    fetchSecure('/tag/update', {
                      method: 'POST',   
                      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },                      
                      body: new URLSearchParams({ tagName: tagName, tagComm: tagComm, tagChck: tagChck })
                    });
                }


            </script>
            ");


          
            //sb.Append("<h2>Steuerungen</h2>");
            //sb.Append("<p>Konfigurierte SPS-Steuerungen</p>");
            //sb.Append("<table>");

            //var plcs = PlcTagManager.Instance.GetAllPlcs();

            //foreach (var plcName in plcs.Keys)
            //    if (plcName.StartsWith('A')) {
            //        sb.AppendLine("<tr>");
            //        sb.AppendLine($"<td>{plcName}</td>");
            //        sb.AppendLine($"<td>{plcs[plcName].CPU.ToString()}<td/>");
            //        sb.AppendLine($"<td>{plcs[plcName].IP}<td/>");
            //        sb.AppendLine($"<td>Rack {plcs[plcName].Rack}<td/>");
            //        sb.AppendLine($"<td>Slot {plcs[plcName].Slot}</td>");
            //        sb.AppendLine($"<td>" +
            //            $"<input type='number' style='width:2rem;' data-name='{plcName}_DB10_DBW2'/>:" +
            //            $"<input type='number' style='width:2rem;' data-name='{plcName}_DB10_DBW4'/>:" +
            //            $"<input type='number' style='width:2rem;' data-name='{plcName}_DB10_DBW6'/>" +
            //            $"</td>");                   
            //        sb.AppendLine("</tr>");
            //    }

            //sb.Append("</table>");

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
                    <link rel='stylesheet' href='/css/style.css'>                    
                    <script src='/js/websocket.js'></script>
                    <script src='/js/excel.js'></script>
                </head>
                <body>");

            sb.Append("<h1>Lesefehler</h1>");
            sb.AppendLine("<a href='/tag/all' class='menuitem'>Gelesene Daten</a>");
            sb.AppendLine("<a href='/source' class='menuitem'>Datenquellen</a>");
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
                sb.Append($"<td>{fail.Time.ToLocalTime()}</td>");
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
            sb.AppendLine("<a href='/tag/all' class='menuitem'>Gelesene Daten</a>");
            sb.AppendLine("<a href='/tag/failures' class='menuitem'>Letzte Lesefehler</a>");

            #region SPS konfigurieren

            sb.Append("<h2>Datenquellen konfigurieren</h2>");
            sb.Append("<hr/><table>");
            sb.Append("<table>");
            sb.Append("<tr><th>Name</th><th>Type</th><th>IP</th><th>Rack</th><th>Slot</th><th>Aktiv</th><th>Bemerkung</th></tr>");

            if (allPlcs.Count == 0)
                sb.AppendLine("<tr><td span='7'>- keine SPS-Konfiguration in der Datenbank gefunden -</td></tr>");

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
                            
                            console.log(data);
                            if (data.Type == 'reload') {
                                alertSuccess(`Operation ${verb} erfolgreich. ${data.Text}`);
                                setTimeout(location.reload(), 5000);                
                            }
                            else
                                message(data.Type, data.Text);
                        }  

                    } catch (error) {
                        console.error(""Fehler beim Abrufen: "", error);
                    }
  
                }
       
                </script>
                ");

            #endregion

            #region zur Zeit konfigurierte Steuerungen
            sb.Append("<h2>Aktive Steuerungen</h2>");           
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
                        $"<input type='number' style='width:2rem;' data-name='{plcName}_DB10_DBW2' disabled/>:" +
                        $"<input type='number' style='width:2rem;' data-name='{plcName}_DB10_DBW4' disabled/>:" +
                        $"<input type='number' style='width:2rem;' data-name='{plcName}_DB10_DBW6' disabled/>" +
                        $"</td>");
                    sb.AppendLine("</tr>");
                }


            sb.Append("</table>");

            #endregion

            #region Informationen zum Host

            sb.Append("<h2>Dieses Gerät</h2>");
            sb.AppendLine("<p>Serveradresse: " + GetIPV4() + "</p>");            
            sb.AppendLine("<p>Serverzeit (lokal): " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "</p>");
            long dbSizeOnDiscMB = Db.Db.GetAllDbSizesInMBytes(out int dbFileCount);
            float dbSizeOnDiscGB = (float)dbSizeOnDiscMB / 1024;
            float avgDbSizeMB = (float)dbSizeOnDiscMB / (float)dbFileCount;
            sb.AppendLine($"<p>Datenbank: {dbFileCount} Dateien mit insgesamt {dbSizeOnDiscMB} MB ({dbSizeOnDiscGB.ToString("F2")} GB, ca. {avgDbSizeMB.ToString("F2")} MB pro Tag)<p>");

            sb.AppendLine("<h3>Laufwerke</h3>");
            sb.AppendLine("<table><tr>" +                
                "<th>Laufwerk</th>" +
                "<th>Bezeichnung</th>" +
                "<th>Art</th>" +
                "<th>Format</th>" +
                "<th>Speicher gesamt</th>" +
                "<th>Speicher frei</th>" +
                "</tr>");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {

                var drives = DriveInfo.GetDrives();
                    
                foreach (DriveInfo drive in drives)
                {
                    bool isCurrentDrive = AppDomain.CurrentDomain.BaseDirectory.StartsWith(drive.Name);
                    long totalSize = 0;
                    long freeSpace = 0;
                    string volLabel = string.Empty;
                    string driveFormat = string.Empty;
                    string driveType = string.Empty;

                    switch (drive.DriveType)
                    {
                        case DriveType.Unknown:
                            driveType = "-unbekannt-";
                            break;
                        case DriveType.NoRootDirectory:
                            driveType = "ohne Wurzelverzeichnis";
                            break;
                        case DriveType.Removable:
                            driveType = "Wechseldatenträger";
                            break;
                        case DriveType.Fixed:
                            driveType = "fest";
                            break;
                        case DriveType.Network:
                            driveType = "Netzwerk";
                            break;
                        case DriveType.CDRom:
                            driveType = "CD-ROM";
                            break;
                        case DriveType.Ram:
                            driveType = "RAM";
                            break;
                        default:
                            break;
                    }

                    try { totalSize = drive.TotalSize / 1074000000; } catch { }
                    try { freeSpace = drive.AvailableFreeSpace / 1074000000; } catch { }
                    try { volLabel = drive.VolumeLabel; } catch { }
                    try { driveFormat = drive.DriveFormat; } catch { }

                    sb.Append($"<tr {(!drive.IsReady ? "style='opacity: 0.5;'" : string.Empty)}{(isCurrentDrive ? "style='font-weight: bold;" : string.Empty)}>");
                    sb.Append($"<td>{drive.Name}</td>");
                    sb.Append($"<td>{volLabel}</td>");
                    sb.Append($"<td>{driveType}</td>");
                    sb.Append($"<td>{driveFormat}</td>");
                    sb.Append($"<td>{totalSize}&nbsp;GB</td>");
                    sb.Append($"<td>{freeSpace}&nbsp;GB</td>");
                    sb.Append("</tr>");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {

                var root = new DriveInfo("/");

                sb.Append($"<tr>");
                sb.Append($"<td>{root.Name}</td>");
                sb.Append($"<td>{root.VolumeLabel}</td>");
                sb.Append($"<td>{root.DriveType}</td>");
                sb.Append($"<td>{root.DriveFormat}</td>");
                sb.Append($"<td>{root.TotalSize / 1024 / 1024 / 1024}&nbsp;GB</td>");
                sb.Append($"<td>{root.AvailableFreeSpace / 1024 / 1024 / 1024}&nbsp;GB</td>");
                sb.Append("</tr>");
            }

            sb.AppendLine("</table>");

            #endregion

            sb.Append(@"
                </body>
                </html>");

            return sb.ToString();
        }

        public static string GetIPV4()
        {
            // Ermittelt den Hostnamen des lokalen Computers
            string hostname = Dns.GetHostName();
            List<string> ipAddresses = [];

            // Holt die IP-Adresse(n) für den Hostnamen
            IPHostEntry hostEntry = Dns.GetHostEntry(hostname);

            // Durchläuft alle gefundenen IP-Adressen (IPv4 und IPv6)
            foreach (IPAddress ipAddress in hostEntry.AddressList)
            {
                // Prüft, ob es eine IPv4-Adresse ist
                if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
                {
                    ipAddresses.Add(ipAddress.ToString());
                }
            }

            return $"{hostname}, {string.Join(", ", ipAddresses)}"; //, {IPAddress.Loopback}
        }

    }
}
