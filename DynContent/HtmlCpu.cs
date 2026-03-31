using Gemini.Db;
using Gemini.Services;
using S7.Net;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace Gemini.DynContent
{
    public static partial class HtmlHelper
    {
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


        internal static async Task<string> ListAllPlcConfigs(bool isReadonly)
        {
            List<PlcConf> allPlcs = Db.Db.SelectAllPlcs();

            StringBuilder sb = new();

            sb.AppendLine(@"<!DOCTYPE html>
                <html lang='de'>
                <head>
                    <meta charset='UTF-8'>
                    <title>Datenquellen</title>                    
                    <link rel='shortcut icon' href='/favicon.ico'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <link rel='stylesheet' href='../css/style.css'>  
                    <script type='module' src='../js/script.js'></script>
                </head>
                <body>");

            sb.AppendLine("<h1>Datenquellen</h1>");
            sb.AppendLine("<a href='/tag/all' class='menuitem'>Gelesene Daten</a>");
            sb.AppendLine("<a href='/tag/failures' class='menuitem'>Letzte Lesefehler</a>");
            sb.AppendLine("<a href='/db/list' class='menuitem'>Datenbanken</a>");

            #region SPS konfigurieren

            sb.AppendLine("<h2>Datenquellen konfigurieren</h2>");
            sb.AppendLine("<hr/><table>");         
            sb.AppendLine("<tr><th>Name</th><th>Type</th><th>IP</th><th>Rack</th><th>Slot</th><th>Aktiv</th><th>Bemerkung</th></tr>");

            if (allPlcs.Count == 0)
                sb.AppendLine("<tr><td span='7'>- keine SPS-Konfiguration in der Datenbank gefunden -</td></tr>");

            foreach (PlcConf plc in allPlcs)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td><input onchange='plcUpdate(\"update\", this);' value='{plc.Name}' {(isReadonly ? "disabled" : string.Empty)}></td>");
                sb.AppendLine($"<td><select onchange='plcUpdate(\"update\", this);' {(isReadonly ? "disabled" : string.Empty)}>");

                foreach (string type in Enum.GetNames<CpuType>())
                    sb.AppendLine($"<option value='{type}' {(type == plc.CpuType.ToString() ? "selected" : string.Empty)}>{type}</option>");

                sb.AppendLine("</select></td>");
                sb.AppendLine($"<td><input onchange='plcUpdate(\"update\", this);' pattern='[0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+' value='{plc.Ip}' {(isReadonly ? "disabled" : string.Empty)}></td>");
                sb.AppendLine($"<td><input onchange='plcUpdate(\"update\", this);' type='number' min='0' size='2' value='{plc.Rack}'  {(isReadonly ? "disabled" : string.Empty)} ></td>");
                sb.AppendLine($"<td><input onchange='plcUpdate(\"update\", this);' type='number' min='0' size='2' value='{plc.Slot}'  {(isReadonly ? "disabled" : string.Empty)} ></td>");
                sb.AppendLine($"<td><input onchange='plcUpdate(\"update\", this); 'type='checkbox' value='{plc.IsActive}'{(plc.IsActive == true ? "checked" : "")} {(isReadonly ? "disabled" : string.Empty)}></td>");
                sb.AppendLine($"<td><input onchange='plcUpdate(\"update\", this);' value='{plc.Comment}' {(isReadonly ? "disabled" : string.Empty)}></td>");
                sb.AppendLine($"<td><input type='hidden' value='{plc.Id}'></td>");

                if (!isReadonly)
                {
                    sb.AppendLine("<td><input onclick='plcUpdate(\"ping\", this);' type='button' value='Ping'></td>");
                    sb.AppendLine("<td><input onclick='plcUpdate(\"create\", this);' type='button' value='Duplizieren'></td>");
                    if (allPlcs.Count > 1)
                        sb.AppendLine($"<td><input onclick='plcUpdate(\"delete\", this);' type='button' class='delete-btn' value='Löschen' {(allPlcs.Count > 1 ? string.Empty : "disabled")}></td>");
                }
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</table>");

            #endregion

            #region zur Zeit konfigurierte Steuerungen
            sb.AppendLine("<h2>Aktive Steuerungen</h2>");
            sb.AppendLine("<style>td {text-align:right;}</style>");
            sb.AppendLine("<table>");

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


            sb.AppendLine("</table>");

            #endregion

            #region Informationen zum Host

            sb.AppendLine("<h2>Dieses Gerät</h2>");
            sb.AppendLine("<p>Serveradresse: " + GetIPV4() + "</p>");
            sb.AppendLine("<p>Serverzeit (lokal): " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "</p>");
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
                "<th>Belegung</th>" +
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

                    sb.AppendLine($"<tr {(!drive.IsReady ? "style='opacity: 0.5;'" : string.Empty)}{(isCurrentDrive ? "style='font-weight: bold;'" : string.Empty)}>");
                    sb.AppendLine($"<td>{drive.Name}</td>");
                    sb.AppendLine($"<td>{volLabel}</td>");
                    sb.AppendLine($"<td>{driveType}</td>");
                    sb.AppendLine($"<td>{driveFormat}</td>");
                    sb.AppendLine($"<td>{totalSize}&nbsp;GB</td>");
                    sb.AppendLine($"<td>{freeSpace}&nbsp;GB</td>");
                    if (totalSize > 0)
                        sb.AppendLine($"<td><meter value='{1-(float)freeSpace/totalSize}'>{1 - (float)freeSpace / totalSize}%</meter></td>");
                    sb.AppendLine("</tr>");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {

                var root = new DriveInfo("/");
                long totalSize = root.TotalSize / 1024 / 1024 / 1024;
                long freeSpace = root.AvailableFreeSpace / 1024 / 1024 / 1024;
                sb.AppendLine($"<tr>");
                sb.AppendLine($"<td>{root.Name}</td>");
                sb.AppendLine($"<td>{root.VolumeLabel}</td>");
                sb.AppendLine($"<td>{root.DriveType}</td>");
                sb.AppendLine($"<td>{root.DriveFormat}</td>");
                sb.AppendLine($"<td>{totalSize}&nbsp;GB</td>");
                sb.AppendLine($"<td>{freeSpace}&nbsp;GB</td>");
                if(totalSize > 0)
                    sb.AppendLine($"<td><meter value='{1 - (float)freeSpace / totalSize}'>{1 - (float)freeSpace / totalSize}%</meter></td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</table>");

            #endregion

            //sb.AppendLine("<script src='../js/script.js'></script>");
            sb.AppendLine(@"                  
                </body>
                </html>");

            return sb.ToString();
        }




    }
}
