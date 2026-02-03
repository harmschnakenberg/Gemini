using Gemini.Models;
using MiniExcelLibs;
using MiniExcelLibs.Attributes;
using MiniExcelLibs.OpenXml;

namespace Gemini.DynContent
{
    public class MiniExcel
    {
        public enum Interval
        {
            Sekunde,
            Minute,
            Viertelstunde,
            Stunde,
            Tag,
            Monat,
            Jahr
        }

        public static string GetTimeFormat(Interval interval)
        {
            return interval switch
            {
                Interval.Sekunde => "yyyy-MM-dd HH:mm:ss",
                Interval.Minute => "yyyy-MM-dd HH:mm",
                Interval.Viertelstunde => "yyyy-MM-dd HH:mm",
                Interval.Stunde => "yyyy-MM-dd HH:'00'",
                Interval.Tag => "yyyy-MM-dd",
                Interval.Monat => "yyyy-MM",
                Interval.Jahr => "yyyy",
                _ => "yyyy-MM-dd HH:mm:ss",
            };
        }

        public static Interval GetTimeFormat(string interval)
        {
            return interval switch
            {
                nameof(Interval.Sekunde) => Interval.Sekunde,
                nameof(Interval.Minute) => Interval.Minute,
                nameof(Interval.Viertelstunde) => Interval.Viertelstunde,
                nameof(Interval.Stunde) => Interval.Stunde,
                nameof(Interval.Tag) => Interval.Tag,
                nameof(Interval.Monat) => Interval.Monat,
                nameof(Interval.Jahr) => Interval.Jahr,
                _ => Interval.Sekunde,
            };
        }



        public static MemoryStream DownloadExcel(Interval interval, Dictionary<string, string> tagNamesAndComment, JsonTag[] jsonTags)
        {
            string timeFormat = GetTimeFormat(interval);
            int i = 0;
            List<DynamicExcelColumn> colStyle = [new("Zeit") { Index = i, Format = timeFormat, Width = 19 }];

            foreach (var tagName in tagNamesAndComment.Keys)            
                colStyle.Add(new(tagName) { Index = ++i, Name = tagNamesAndComment[tagName]});
            
            var config = new OpenXmlConfiguration
            {
                DynamicSheets = [new("usersSheet") { Name = "Werte", State = SheetState.Visible }],
                DynamicColumns = [.. colStyle]
            };

            //Console.WriteLine($"Es wird versucht {jsonTags.Length} Tags in Excel zu speichern..");
            var groups = jsonTags.GroupBy(t => DateTime.Parse(t.T.ToString(timeFormat))).OrderBy(o => o.Key);
            var values = new List<Dictionary<string, object?>>();

            foreach (var group in groups)
            {
                var cols = new Dictionary<string, object?>
                {
                    { "Zeit", group.Key }
                };

                foreach (var tagName in tagNamesAndComment.Keys)
                {
                    JsonTag? x = group.Where(o => o.N == tagName).FirstOrDefault();
                    cols.Add(tagName, x?.V);
                }

                values.Add(cols);
            }

            var sheets = new Dictionary<string, object>
            {
                ["usersSheet"] = values
            };


            var memoryStream = new MemoryStream();
            memoryStream.SaveAs(sheets, configuration: config);
            memoryStream.Seek(0, SeekOrigin.Begin);
            return memoryStream;
        }
    }
}
