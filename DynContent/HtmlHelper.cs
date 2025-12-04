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
                    <link rel='stylesheet' href='/css/atmos.css'>
                    <link rel='stylesheet' href='/css/input.css'>
                    <script src='/js/websocket.js'></script>
                </head>
                <body>");

            sb.Append("<h2>Datenpunkte</h2>");
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
                if (plcName.StartsWith('A'))
                    sb.Append($"<li><span style='display:inline-block;width:3rem'>{plcName}:</span>{plcs[plcName].CPU.ToString()}, {plcs[plcName].IP}, Rack {plcs[plcName].Rack}, Slot {plcs[plcName].Slot}</li>");

            sb.Append("</ul>");

            sb.Append(@"
                </body>
                </html>");

            return sb.ToString();
        }

    }
}
