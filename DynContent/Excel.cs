using Gemini.Models;
using OfficeOpenXml;
using System.IO;


namespace Gemini.DynContent
{
    public class Excel
    {
        public enum Interval
        {
            Sekunde,
            Minute,
            Stunde,
            Tag,
            Monat,
            Jahr
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


            #region Format Zeitspalte
            string timeFormat = "yyyy-MM-dd HH:mm:ss";

            switch (interval)
            {
                case Interval.Sekunde:
                    timeFormat = "yyyy-MM-dd HH:mm:ss";
                    break;
                case Interval.Minute:
                    timeFormat = "yyyy-MM-dd HH:mm";
                    break;
                case Interval.Stunde:
                    timeFormat = "yyyy-MM-dd HH:00";
                    break;
                case Interval.Tag:
                    timeFormat = "yyyy-MM-dd";
                    break;
                case Interval.Monat:
                    timeFormat = "yyyy-MM";
                    break;
                case Interval.Jahr:
                    timeFormat = "yyyy";
                    break;
            }

            #endregion

            #region Add Data

            var groups = jsonTags.GroupBy(t => DateTime.Parse(t.T.ToString(timeFormat)));
           
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
