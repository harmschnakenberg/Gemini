using Gemini.Services;
using Microsoft.Extensions.Primitives;
using System.Text;

namespace Gemini.DynContent
{
    public class HtmlHelper
    {
      

        internal static async Task<string> ListAllTags() {             
            
            StringBuilder sb = new();

            sb.Append(@"<!DOCTYPE html>
                <html lang='de'>
                <head>
                    <meta charset='UTF-8'>
                    <title>Alle Werte</title>
                    <link rel='icon' type='image/x-icon' href='/favicon.ico'>
                    <link rel='shortcut icon' href='/favicon.ico'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <link rel='stylesheet' href='/css/style.css'>                    
                    <script src='/js/websocket.js'></script>
                </head>
                <body>");

            sb.Append("<h1>Datenpunkte</h1>");
            sb.Append("<div class='sollwerte'>");
            sb.Append("<h2>Alle momentan aufgezeichneten Werte</h2>");

            Dictionary<string, string> allTags = await Db.Db.GetDbTagNames(DateTime.UtcNow, 1);

            foreach (string tagName in allTags.Keys)
            {
                sb.Append($"<label>{allTags[tagName]}</label>");
                sb.Append($"<input data-name='{tagName}' data-unit='' disabled>");
                sb.Append("<div>&nbsp;</div>");
            }
            sb.Append("</div>");

            sb.Append("<h2>Steuerungen</h2>");
            sb.Append("<p>Konfigurierte SPS-Steuerungen</p>");
            sb.Append("<ul>");

            var plcs = PlcTagManager.Instance.GetAllPlcs();

            foreach (var plcName in plcs.Keys)
                if (plcName.StartsWith('A')) {
                    sb.AppendLine("<li>");
                    sb.AppendLine($"<span style='display:inline-block;width:3rem'>{plcName}:</span>");
                    sb.AppendLine($"{plcs[plcName].CPU.ToString()}, {plcs[plcName].IP}, Rack {plcs[plcName].Rack}, Slot {plcs[plcName].Slot}");
                    //sb.AppendLine($"<span><input type='hidden' id='{plcName}hour' data-name='{plcName}_DB10_DBW2'/>");
                    //sb.AppendLine($"<input type='hidden' id='{plcName}hour' data-name='{plcName}_DB10_DBW4'/>");
                    //sb.AppendLine($"<input type='hidden' id='{plcName}hour' data-name='{plcName}_DB10_DBW6'/>");
                    //sb.AppendLine("</li>");

                }

            /*
             * <div style="position:absolute; right:1rem; top:1rem;">
        <input type="hidden" id="hour" data-name='A01_DB10_DBW2' />
        <input type="hidden" id="min" data-name='A01_DB10_DBW4' />
        <input type="hidden" id="sec" data-name='A01_DB10_DBW6' />
        SPS-Zeit <span id="time"></span>
        <script>
            function clock() {
                h = document.getElementById('hour').value;
                m = document.getElementById('min').value;
                s = document.getElementById('sec').value;                
                document.getElementById('time').innerHTML = h.padStart(2, "0") + ':' + m.padStart(2, "0") + ':' + s.padStart(2, "0");
            }
            window.setInterval(clock, 1000);
        </script>
    </div>
            */
            sb.Append("</ul>");

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
