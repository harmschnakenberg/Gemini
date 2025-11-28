using Gemini.Services;
using Microsoft.Extensions.Primitives;
using System.Text;

namespace Gemini.DynContent
{
    public class HtmlHelper
    {
        internal static async Task<string> RequestExcelForm()
        {
            StringBuilder sb = new();

            sb.Append(@"<!DOCTYPE html>
                <html lang='de'>
                <head>
                    <meta charset='UTF-8'>
                    <title>Tabellenexport</title>
                    <link rel='shortcut icon' href='/favicon.ico'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <link rel='stylesheet' href='/css/atmos.css'>
                    <link rel='stylesheet' href='/css/input.css'>
                    <script src='/js/excelform.js'></script>
                </head>
                <body>");

            sb.Append("<h2>Datentabelle exportieren</h2>");
            sb.Append(@"<p style='padding: 0.5rem;'>
                Hier können Sie aufgezeichnete Daten herunterladen. 
               <br/>
                
            </p>");

            Dictionary<string, string> allTags = await Db.Db.GetDbTagNames(DateTime.UtcNow, 3);
            StringBuilder tagList = new("<datalist id='comments'>\r\n");

            foreach (string tagName in allTags.Keys)
            {
                string comment = allTags[tagName]?.Length > 3 ? allTags[tagName] : tagName;
                tagList.AppendLine($"  <option value='{comment}'>");
            }
            tagList.Append("</datalist>");


            sb.Append($@"
                <form id='myForm' method='post' action='/excel'>
                 
                    <fieldset>
                     <legend>Zeitraum:</legend>
                      <p>
                        Wählen Sie den Zeitraum, aus dem die Daten entnommen werden sollen, sowie den Wertintervall,<br/>
                        d.h. mit welchem Zeitabstand die Werte in die Tabelle eingetragen werden sollen.
                      </p>
                      <label for='start'>Beginn</label>
                      <input class='myForm' type='datetime-local' id='start' name='start'>
                      <label for='end'>Ende</label>
                      <input class='myForm' type='datetime-local' id='end' name='end'>
                      <label for='interval'>Werteintervall</label>
                      <select class='myForm' id='interval' name='interval'>
                        <option value='0'>Sekunde</option>
                        <option value='1' selected>Minute</option>
                        <option value='2'>Stunde</option>
                        <option value='3'>Tag</option>
                        <option value='4'>Monat</option>
                        <option value='5'>Jahr</option>
                      </select>
                    </fieldset>

                  <hr/>
              
                  <fieldset>
                    <legend>Zuordnung Tabellenspalten:</legend>
                    <p>
                        Ordnen Sie hier den Spalten der Tabelle Werte zu, indem Sie Datenpunkte in die Liste eintragen.<br/>
                        Der genaue Wortlaut wird während der Eingabe vorgeschlagen. Ungültige Bezeichner werden ignoriert.<br/>
                        Die erste Spalte 'A' enthält immer den Zeitstempel und kann nicht geändert werden.
                    </p>
                    <ol type='A'>
                      <li><span class='colForTable'>Zeitspalte</span></li>
                      <li id='li0' ondrop='dropHandler(event)'   ondragover='dragoverHandler(event)'>
                        <input draggable='true' ondragstart='dragstartHandler(event)' class='colForTable' list='comments' name='col0' id='col0'>
                      </li>
                    </ol>
                  </fieldset>
                  
                  <input class='myForm'type='submit' value='Excel-Tabelle herunterladen'>

                  ");
            sb.Append(tagList);
           /* sb.Append(@"
                <script>
                    function addCol(){
                        let form = document.forms['myForm'];
                        const i = document.forms['myForm'].getElementsByTagName('li').length;

                        var y = document.createElement('LI');
                        y.setAttribute('id', 'li' + i);
                        y.addEventListener('drop', (event) => { dropHandler(event) });
                        y.addEventListener('dragover', (event) => { event.preventDefault(); });
                        var x = document.createElement('INPUT');
                          x.addEventListener('dragstart', (event) => { dragstartHandler(event) });
                          x.setAttribute('dragable', 'true');
                          x.setAttribute('list', 'comments');
                          x.setAttribute('name', 'col' + i);
                          x.setAttribute('id', 'col' + i);
                          x.classList.add('colForTable');
                          document.getElementById('myForm').getElementsByTagName('ol')[0].appendChild(y).appendChild(x);                       
                    }

                    function dragstartHandler(ev) {
                      ev.dataTransfer.setData('text', ev.target.id);
                    }

                    function dragoverHandler(ev) {
                      ev.preventDefault();
                    }

                    function dropHandler(ev) {
                      ev.preventDefault();
                      const data = ev.dataTransfer.getData('text');
                      ev.target.appendChild(document.getElementById(data));
                    }

                </script> //*/
                sb.Append(@" <style>
                    .colForTable {
                        display: block;
                        padding: 0.2rem;
                        font-size: 1em;                        
                    }

                    .myForm {                        
                        font-family: Arial, Helvetica, sans-serif;
                        padding: 0.2rem;
                        margin: 1rem;
                        font-size: 1em;
                    }

                    li {
                      width: 350px;
                      height: 30px;
                      padding: 10px;
                      border: 1px solid #aaaaaa;
                    }

                </style>
                </form>
                <button onclick='addCol();' >+</button>");

            sb.Append(@"
                </body>
                </html>");

            return sb.ToString();
        }


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
