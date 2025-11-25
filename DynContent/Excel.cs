using Gemini.Models;
using OfficeOpenXml;
using System;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace Gemini.DynContent
{
    public class Excel
    {
        internal void CreateExcelWb(DateTime start, DateTime end, Dictionary<string, string> tagNamesAndComment, JsonTag[] jsonTags)
        {
            
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            #region  Pfade
            
            string excelFileName = "Werte_" + start.ToString("yyyyMMdd") + "_" + end.ToString("yyyyMMdd");
            string excelDir = Path.Combine(Db.Db.AppFolder, "excel");

            if (!Directory.Exists(excelDir))
                Directory.CreateDirectory(excelDir);

            string excelPath = Path.Combine(excelDir, excelFileName);
            
            #endregion

            using (var package = new ExcelPackage(new FileInfo(excelPath)))
            {
                package.Workbook.Properties.Author = "Kreutzträger Kältetechnik";
                package.Workbook.Properties.Title = "Werte Kälteanlage";
                package.Workbook.Properties.Subject = "Datenlogger Werte";
                package.Workbook.Properties.Keywords = "Kälteanlage, Datenlogger, Werte";
                var wb = package.Workbook;
                var ws = wb.Worksheets.Add("Werte");

                // Add headers
                ws.Cells[1, 1].Value = "Zeitstempel";
                ws.Cells[1, 2].Value = "Tag Name";
                ws.Cells[1, 3].Value = "Wert";
                
                int row = 2;
                foreach (var tag in jsonTags)
                {
                    ws.Cells[row, 1].Value = tag.T.ToString("yyyy-MM-dd HH:mm:ss");
                    ws.Cells[row, 2].Value = tag.N;
                    ws.Cells[row, 3].Value = tag.V;
                   
                    row++;
                }
                package.Save();
                    
                
            }

        }
    }
}
