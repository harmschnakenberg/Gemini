using Gemini.Models;
using OfficeOpenXml;
using System.IO;
using System.Runtime.CompilerServices;
using static Gemini.DynContent.Excel;


namespace Gemini.DynContent
{
    /// <summary>
    /// Alternative? Bibliothek: https://github.com/mini-software/MiniExcel
    /// 
    /// </summary>
    public class Excel
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
                Interval.Stunde => "yyyy-MM-dd HH:00",
                Interval.Tag => "yyyy-MM-dd",
                Interval.Monat => "yyyy-MM",
                Interval.Jahr => "yyyy-MM",
                _ => "yyyy-MM-dd HH:mm:ss",
            };
        }

       

        internal static async Task<MemoryStream> CreateExcelWb(Interval interval, Dictionary<string, string> tagNamesAndComment, JsonTag[] jsonTags)
        {
            
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var outputStream = new MemoryStream();
            //            using var package = new ExcelPackage(new FileInfo(excelPath));
            using var package = new ExcelPackage(outputStream);
            package.Workbook.Properties.Author = "Kreutzträger Kältetechnik";
            package.Workbook.Properties.Title = "Werte Kälteanlage";
            package.Workbook.Properties.Subject = "Datenlogger Werte";
            package.Workbook.Properties.Keywords = "Kälteanlage, Datenlogger, Werte";
            var wb = package.Workbook;
            var ws = wb.Worksheets.Add("Werte");

            #region Überschriften formatieren
            //Zeitspalte
            ws.Cells[1, 1].Value = "Zeitstempel";

            // Add headers
            for (int i = 0; i < tagNamesAndComment.Count; i++)
            {
                var tagName = tagNamesAndComment.Keys.ElementAt(i);
                var comment = tagNamesAndComment[tagName];
                var cell = ws.Cells[1, i + 2];
                cell.Value = comment;

                if (tagName != comment)
                {
                    var commentObj = cell.AddComment(tagName, "Kreutzträger");
                    commentObj.AutoFit = true;
                }

            }

            #endregion

                        
            #region Add Data


            string timeFormat = GetTimeFormat(interval);
            var groups = jsonTags.GroupBy(t => DateTime.Parse(t.T.ToString(timeFormat))).OrderBy(o => o.Key);
           
            int row = 1;
            foreach (var group in groups)
            {
                row++;
                DateTime t = group.Key;
                var timeCell = ws.Cells[row, 1];
                timeCell.Value = t.ToOADate();
                timeCell.Style.Numberformat.Format = "m/d/yy h:mm"; // "dd.mm.yyyy HH:mm";
                

                foreach (var tag in group)
                {
                    int col = tagNamesAndComment.Keys.ToList().IndexOf(tag.N) + 2;
                    ws.Cells[row, col].Value = tag.V;
                }
            }

            #endregion

            ws.Cells[ws.Dimension.Rows, 1].AutoFitColumns(); // AutoFit Zeitspalte

            //var x = new MemoryStream();
            //package.SaveAs(x);
            //await x.FlushAsync();

            package.Save();
            outputStream.Position = 0;

            //await outputStream.FlushAsync();
            return outputStream;
        }
    }
}
