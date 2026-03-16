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
            #region Tags für Kurve 1
            StringBuilder tags1 = new("const tags1 = new Map([");
            
            if (chartConfig.Chart1Tags is not null)
                foreach (var t in chartConfig.Chart1Tags)
                   tags1.AppendLine($" ['{t.Key}', '{t.Value}'],");

            tags1.AppendLine("]);\r\n");
            #endregion

            #region Tags für Kurve 2
            StringBuilder tags2 = new("const tags2 = new Map([");

            if (chartConfig.Chart2Tags is not null)
                foreach (var t in chartConfig.Chart2Tags)
                    tags2.AppendLine($" ['{t.Key}', '{t.Value}'],");

            tags2.AppendLine("]);\r\n");
            #endregion 

            string startVal = start == System.DateTime.MinValue ? string.Empty : $"value='{start:yyyy-MM-ddTHH:mm:ss}'";
            string endVal = end == System.DateTime.MinValue ? string.Empty : $"value='{end:yyyy-MM-ddTHH:mm:ss}'";

            Dictionary<string, string> changeMap = new()
            {
                { "<title>Demo Kurve</title>", $"<title>{chartConfig.Caption}</title>" },
                { "<h1>Demo Kurvendarstellung</h1>", $"<h1>{chartConfig.Caption}</h1>" },
                { "<p>Beispiel Kurve</p>", $"<p>{chartConfig.SubCaption}</p>" },
                { "id='start' name='start'", $"id='start' {startVal} name='start'"},
                { "id='end' name='end'", $"id='end' {endVal} name='end'"},
                { "const tags1 = new Map();", tags1.ToString() },
                { "const tags2 = new Map();", tags2.ToString() },
            };

            StringBuilder sb = new(File.ReadAllText("wwwroot/html/chart.html"));
        
            foreach (var key in changeMap.Keys)
                sb.Replace(key, changeMap[key]);
            
            return sb.ToString();
        }

    }
}
