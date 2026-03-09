using Gemini.Models;
using System.Text;

namespace Gemini.DynContent
{
    public static partial class HtmlHelper
    {
        #region Hilfs-Methoden
     

        #endregion


        /// <summary>
        /// Generates a complete HTML document that displays two interactive charts with user controls, based on the
        /// provided caption and chart tag data.
        /// </summary>
        /// <remarks>The generated HTML includes embedded JavaScript for rendering charts using Chart.js
        /// and related libraries. The page provides controls for adjusting the displayed time range, exporting data,
        /// and interacting with the charts (such as zooming and panning). The tag dictionaries determine which data
        /// series are shown in each chart. This method is intended for internal use to dynamically create chart pages
        /// based on runtime data.</remarks>
        /// <param name="caption">The title to display at the top of the generated chart page.</param>
        /// <param name="chart1Tags">A dictionary containing key-value pairs that define the data tags for the first chart. Each entry represents
        /// a data series or metric to be visualized.</param>
        /// <param name="chart2Tags">A dictionary containing key-value pairs that define the data tags for the second chart. Each entry
        /// represents a data series or metric to be visualized.</param>
        /// <returns>A string containing the full HTML markup for a web page that renders two dynamic charts and associated
        /// controls.</returns>
        internal static string DynChart(ChartConfig chartConfig, System.DateTime start, System.DateTime end)
        {

            StringBuilder sb = new();

            sb.AppendLine(@"
                <!DOCTYPE html>
                <html lang='de'>
                    <head>
                    <meta charset='UTF-8'>");
            sb.AppendLine(@$"
                    <title>{chartConfig.Caption}</title>");
            sb.AppendLine(@"
                    <link rel='shortcut icon' href='/favicon.ico'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <link rel='stylesheet' href='../css/style.css'>                    
                    <link rel='stylesheet' href='https://fonts.googleapis.com/icon?family=Material+Icons'>
                    <script src='https://cdn.jsdelivr.net/npm/chart.js@4.5.1'></script>
                    <script src='https://cdn.jsdelivr.net/npm/luxon@^2'></script>
                    <script src='https://cdn.jsdelivr.net/npm/chartjs-adapter-luxon@^1'></script>
                    <script src='https://cdn.jsdelivr.net/npm/hammerjs@2.0.8/hammer.min.js'></script>
                    <script src='https://cdnjs.cloudflare.com/ajax/libs/chartjs-plugin-zoom/2.2.0/chartjs-plugin-zoom.min.js' integrity='sha512-FRGbE3pigbYamZnw4+uT4t63+QJOfg4MXSgzPn2t8AWg9ofmFvZ/0Z37ZpCawjfXLBSVX2p2CncsmUH2hzsgJg==' crossorigin='anonymous' referrerpolicy='no-referrer'></script>
                    <script src='../js/script.js'></script>
                </head>
                <body>                             
            ");

            sb.AppendLine(@$"
                        <h1>{chartConfig.Caption}</h1>");
            sb.AppendLine(@$"
                        <p>{chartConfig.SubCaption}</p>");
            sb.AppendLine(@"
                        <span id='customspinner'>Der Browser berechnet die Kurven</span>");

            sb.AppendLine(@"
                    <canvas id='myChart1' class='chart' style='background-color: rgb(70, 70, 70); min-width: 50vw; max-width: 90vw; height: 30vh; max-height: 40vh; '></canvas>
                    
                    <div class='container controls' style='max-width: fit-content; margin-left: auto; margin-right: auto;'>
                        <div class='myButton' id='chartTimespan'></div>");
            sb.AppendLine(@$"
                        <input type='datetime-local' id='start' name='start' onfocusout='loadAllCharts();' {(start == System.DateTime.MinValue ? string.Empty : $"value='{start:yyyy-MM-ddTHH:mm:ss}'")}>");
            sb.AppendLine(@$"
                        <input type='datetime-local' id='end' name='end' onfocusout='loadAllCharts();' {(end == System.DateTime.MinValue ? string.Empty : $"value='{end:yyyy-MM-ddTHH:mm:ss}'")}>");

            sb.AppendLine($@"
                        <select class='myButton' id='interval'>
                            <option value='0'>&#x26C1;</option>
                            <option value='1' selected>&#x26C0;</option>            
                        </select>
            ");

            sb.AppendLine(@"
                        <button class='myButton' onclick='setDatesHours('start', 'end', 8);loadAllCharts();'><i class='material-icons'>schedule</i>8</button>
                        <button class='myButton' onclick='setDatesHours('start', 'end',24);loadAllCharts();'><i class='material-icons'>schedule</i>24</button>
                        <button class='myButton' onclick='excelExport(  'start', 'end', 0, getAllTags([tags1,tags2]));'><i class='material-icons'>save</i></button>
                        <button class='myButton' onclick='zoom(['myChart1', 'myChart2'], 1.2);'><i class='material-icons'>zoom_in</i></button>
                        <button class='myButton' onclick='zoom(['myChart1', 'myChart2'], 0.8);'><i class='material-icons'>zoom_out</i></button>
                        <button class='myButton' onclick='resetZoom(['myChart1', 'myChart2'])'><i class='material-icons'>center_focus_weak</i></button>
                        <button class='myButton' onclick='panX(['myChart1', 'myChart2'], 100);'><i class='material-icons'>arrow_left</i></button>
                        <button class='myButton' onclick='panX(['myChart1', 'myChart2'], -100);'><i class='material-icons'>arrow_right</i></button>
                        <!-- <button class='myButton' onclick='toggleStatusChart('myChart2', tags2)'><i style='color:red;' class='material-icons'>legend_toggle</i></button> -->
                    </div>

                    <canvas id='myChart2' class='chart' style='background-color: rgb(70, 70, 70); min-width: 50vw; max-width: 90vw; height: 30vh; max-height: 40vh; '></canvas>

                    <div id='progressContainer' style='position: absolute; top:10px; left: 40%; margin: auto; display: none; '>
                        <label for='progressBar'>Verarbeitung:</label>
                        <progress id='progressBar' value='0' max='100' style='width: 300px;'></progress>
                        <span id='progressText'>0%</span>
                    </div>

                    <div id='rawDataLinks' style='position:sticky; bottom:0.5rem; left:0.5rem;'></div>
            ");

            sb.AppendLine(@"<script>
                    const tags1 = new Map([");

            if (chartConfig.Chart1Tags is not null)
                foreach (var t in chartConfig.Chart1Tags)
                    sb.AppendLine($" ['{t.Key}', '{t.Value}'],");

            sb.AppendLine("]);\r\n");

            sb.AppendLine(@"const tags2 = new Map([");

            if (chartConfig.Chart2Tags is not null)
                foreach (var t in chartConfig.Chart2Tags)
                    sb.AppendLine($" ['{t.Key}', '{t.Value}'],");

            sb.AppendLine("]);\r\n");

            sb.AppendLine(@"
                initChart('myChart1', false);
                initChart('myChart2', true);
                if (!getTimeParams()){
                    setDatesHours('start', 'end', 8);
                }
                loadAllCharts();
            ");

            sb.AppendLine(@"
                function getTimeParams() {
                    const url = new URL(window.location.href);
                    const s = url.searchParams.get('start');
                    const e = url.searchParams.get('end');
                    if (!s || !e || isNaN(new Date(s)) || isNaN(new Date(e))) {
                        console.warn(`Parameter fehlerhaft: start ${s}, end ${e}`);
                        return false;
                    }
                    document.getElementById('start').value = s;
                    document.getElementById('end').value = e;
                    return true;
                }
            ");

            sb.AppendLine(@"
                 function setTimeParams() {
                    const url = new URL(window.location.href);
                    url.searchParams.set('start', document.getElementById('start').value);
                    url.searchParams.set('end', document.getElementById('end').value);
                    window.history.pushState(null, '', url.toString())
                }            
            ");

            sb.AppendLine(@"
                function loadAllCharts() {
                    if (checkDuration('start', 'end', 'chartTimespan')) {
                        loadChart('myChart1', 'start', 'end', 'interval', tags1);
                        loadChart('myChart2', 'start', 'end', 'interval', tags2);
                        setTimeParams();
                    }   
                }

                function checkDuration(startId, endId, outputId) {
                    const start = new Date(document.getElementById(startId).value);
                    const end = new Date(document.getElementById(endId).value);
                    const duration = (end - start) / 86400000;
                    const obj = document.getElementById(outputId)
                    obj.innerHTML = duration.toFixed(0) + ' Tage';

                    if (duration > 91 || duration < 0) {
                        obj.style.color = 'red';
                        return false;
                    } else {
                        obj.style.color = 'inherit';
                        return true;              
                    }
                }
            ");

            sb.AppendLine(@"</script> 
              </body>
            </html>");

            return sb.ToString();
        }


    }
}
