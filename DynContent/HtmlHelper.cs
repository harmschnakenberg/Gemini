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
                    <link rel='shortcut icon' href='/favicon.ico'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <link rel='stylesheet' href='/css/atmos.css'>
                    <link rel='stylesheet' href='/css/input.css'>
                    <script src='/js/websocket.js'></script>
                </head>
                <body>");

            sb.Append("<h2>Daten exportieren</h2>");

            Dictionary<string, string> allTags = await Db.Db.GetDbTagNames(DateTime.UtcNow, 1);
            StringBuilder tagList = new StringBuilder("<datalist id='comments'>");

            foreach (string tagName in allTags.Keys)
            {
                string comment = allTags[tagName]?.Length > 3 ? allTags[tagName] : tagName;
                tagList.AppendLine($" <option value='{comment}'>");
            }
            tagList.Append("</datalist>");


            sb.Append($@"
                <form id='myForm' method='post' action='/excel'>
                  <input class='colForTable' style='display:inline-block;' type='submit' value='Excel-Tabelle erstellen'>
                  <label for='start'>Beginn</label>
                  <input class='colForTable' style='display:inline-block;' type='datetime-local' id='start' name='start'>
                  <label for='end'>Ende</label>
                  <input class='colForTable' style='display:inline-block;' type='datetime-local' id='end' name='end'>
                  <hr/>
                  <ol type='A'>
                   <li><span class='colForTable'>Zeitspalte</span></li>
                   <li><input class='colForTable' list='comments' name='col0'></li>
                  </ol>
                  ");
            sb.Append(tagList); 
            sb.Append(@"
                <script>
                    function addCol(){
                        let form = document.forms['myForm'];
                        const iName = 'col' + document.forms['myForm'].getElementsByTagName('li').length;

                        var y = document.createElement('LI');
                        var x = document.createElement('INPUT');
                          x.setAttribute('list', 'comments');
                          x.setAttribute('name', iName);
                          x.classList.add('colForTable');
                          document.getElementById('myForm').getElementsByTagName('ol')[0].appendChild(y).appendChild(x);                       
                    }
                </script>
                <style>
                    .colForTable {
                        display: block;
                        padding: 0.2rem;
                        font-size: 1em;
                        
                    }
                </style>
                </form>
                <button onclick='addCol();' >+</button>");


            sb.Append("<h2>Datenpunkte</h2>");
            sb.Append("<div class='sollwerte'>");
            sb.Append("<h2>Alle momentan gelesenen Werte</h2>");


           

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
