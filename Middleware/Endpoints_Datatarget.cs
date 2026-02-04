using Gemini.Db;
using Gemini.Models;
using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Gemini.Middleware
{
    public static partial class Endpoints
    {

        #region Kurven

        private static IResult Chart()
        {            
            var file = File.ReadAllText("wwwroot/html/chart.html");
            return Results.Content(file, "text/html");
        }

        #endregion


        #region Excel


        private static IResult GetExcelForm()
        {
            //ctx.Response.StatusCode = 200;
            //ctx.Response.ContentType = "text/html";
            //var file = File.ReadAllText("wwwroot/html/excel.html", Encoding.UTF8);
            //await ctx.Response.WriteAsync(file);
            //await ctx.Response.CompleteAsync();

            var file = File.ReadAllText("wwwroot/html/excel.html");
            return Results.Content(file, "text/html");
        }

        /// <summary>
        /// Processes an HTTP request to generate and return an Excel file containing data for the specified tags and
        /// time interval.
        /// </summary>
        /// <remarks>The method expects the form parameters 'start', 'end', 'interval', and 'tags' to be
        /// present and valid. If any parameter is missing or invalid, an error message is returned instead of an Excel
        /// file. The generated Excel file contains data for the specified tags within the given time range and
        /// interval. The response is sent as an attachment with the appropriate content type for Excel files.</remarks>
        /// <param name="ctx">The HTTP context for the current request. The request must include form parameters 'start' and 'end' (as
        /// date/time strings), 'interval' (as an integer), and 'tags' (as a JSON array of tag objects).</param>
        /// <returns>A task that represents the asynchronous operation. The response is written directly to the HTTP context as
        /// an Excel file attachment if the parameters are valid; otherwise, a plain text error message is returned.</returns>
        private static async Task ExcelDownload(HttpContext ctx)
        {
            string jsonString = ctx.Request.Form["tags"].ToString() ?? string.Empty;
            //Console.WriteLine("/excel : " + jsonString);

            if (
            !DateTime.TryParse(ctx.Request.Form["start"], out DateTime start) ||
            !DateTime.TryParse(ctx.Request.Form["end"], out DateTime end) ||
            !int.TryParse(ctx.Request.Form["interval"], out int intInterval) ||
            jsonString?.Length < 3
            )
            {
                string msg = $"Mindestens ein Übergabeparameter war nicht korrekt.\r\n";

#if DEBUG
                msg +=
                $"start: '{ctx.Request.Form["start"]}'\r\n" +
                $"end: '{ctx.Request.Form["end"]}'\r\n" +
                $"interval: '{ctx.Request.Form["interval"]}'\r\n" +
                $"tags: '{ctx.Request.Form["tags"]}'\r\n";
#endif
                ctx.Response.ContentType = "text/plain";
                await ctx.Response.WriteAsync(msg);
                await ctx.Response.CompleteAsync();
                return;
            }

            //Console.WriteLine($"Rohempfang:\r\n'{jsonString}'\r\n");

            JsonTag[] tags = JsonSerializer.Deserialize(jsonString ?? string.Empty, AppJsonSerializerContext.Default.JsonTagArray) ?? [];
            Dictionary<string, string> tagsAndCommnets = tags.ToDictionary(t => t?.N ?? string.Empty, t => t.V?.ToString() ?? string.Empty);
            string[] tagNames = [.. tagsAndCommnets.Keys];
            Gemini.DynContent.MiniExcel.Interval interval = (Gemini.DynContent.MiniExcel.Interval)intInterval;

#if DEBUG
            //Console.WriteLine($"Interval = {interval}");
#endif
            //JsonTag[] obj = await Db.GetDataSet2(tagNames!, start, end);
            //MemoryStream fileStream = await Excel.CreateExcelWb((Excel.Interval)interval, tagsAndCommnets, obj);

            JsonTag[] jsonTags = await Db.Db.GetDataSet(tagNames!, start, end, interval);
            MemoryStream fileStream = Gemini.DynContent.MiniExcel.DownloadExcel(interval, tagsAndCommnets, jsonTags);

            string excelFileName = $"Kreu_{start:yyyyMMdd}_{end:yyyyMMdd}_{interval}_{DateTime.Now.TimeOfDay.TotalSeconds:0000}.xlsx";

            ctx.Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            ctx.Response.Headers.ContentDisposition = $"attachment; filename={excelFileName}";
            ctx.Response.ContentLength = fileStream.Length;

            await fileStream.CopyToAsync(ctx.Response.Body);
            await ctx.Response.CompleteAsync();
        }

        private static IResult GetExcelConf(HttpContext context)
        {
            List<TagCollection> ChartCollections = Db.Db.GetTagCollections();

            foreach (var item in ChartCollections)
            {
                Console.WriteLine($"{item.Id} {item.Name} {item.Author} {item.Tags[0].TagName}");
            }

            //HIER FEHLER
            string json = JsonSerializer.Serialize([.. ChartCollections], AppJsonSerializerContext.Default.TagCollectionArray);
            Console.WriteLine("JSON " + json);

            //return Results.Json(json, AppJsonSerializerContext.Default.TagCollectionArray);
            return Results.Content(json, "application/json");

        }

        private static IResult ExcelConfCreate(TagCollection x, HttpContext ctx, ClaimsPrincipal user)
        {
            bool isAuthorized = user.IsInRole(Role.User.ToString()) || user.IsInRole(Role.Admin.ToString());
            if (!isAuthorized) //Nur Administratoren und User dürfen Konfigurationen speichern
                return Results.Unauthorized();

            if (x is null)
                return Results.NoContent();

            string author = user.Identity?.Name ?? "-unbekannt-";
#if DEBUG
            Console.WriteLine($"ExcelConfCreate()\r\n" +
                $"name={x?.Name}\r\n" +
                $"author={x?.Author}\r\n" +
                $"startStr={x?.Start}\r\n" +
                $"endStr= {x?.End}\r\n" +
                $"intervalStr={x?.Interval}\r\n" +
                $"tagsStr={x?.Tags}\r\n");
#endif
            int result = Db.Db.CreateChartconfig(x!.Name, author, x.Start, x.End, (Gemini.DynContent.MiniExcel.Interval)x.Interval, x.Tags);

            if (result > 0)
                return Results.Ok();
            else
                return Results.Conflict();
        }

        private static IResult ExcelConfDelete(int id, HttpContext ctx, ClaimsPrincipal user)
        {
            bool isAdmin = user.IsInRole(Role.Admin.ToString());
            if (!isAdmin) // Nur Admins können Konfigurationen löschen
                return Results.Unauthorized();

            int result = Db.Db.DeleteChartconfig(id);
            if (result > 0)
                return Results.Ok();
            else
                return Results.Conflict();
        }


        #endregion

        #region Datenbank 


        private static IResult DbDownload(HttpContext ctx)
        {
            DateTime startUtc = DateTime.UtcNow.AddDays(-8);
            DateTime endUtc = DateTime.UtcNow;

            if (ctx.Request.Form.TryGetValue("start", out var startStr) && DateTime.TryParse(startStr, out DateTime s))
                startUtc = s.ToUniversalTime();

            if (ctx.Request.Form.TryGetValue("end", out var endStr) && DateTime.TryParse(endStr, out DateTime e))
                endUtc = e.ToUniversalTime();

            Console.WriteLine($"DB Download von {startUtc} bis {endUtc}");

            using var memoryStream = new MemoryStream();
            using (ZipArchive archive = new(memoryStream, ZipArchiveMode.Create, true, Encoding.UTF8))
            {
                foreach (var dbPath in Db.Db.GetDatabasePaths(startUtc, endUtc))
                    try
                    {
                        if (File.Exists(dbPath))
                            archive.CreateEntryFromFile(dbPath, Path.GetFileName(dbPath), CompressionLevel.Fastest);
                    }
                    catch { /* Nichts unternehmen? */ }
            }
            
            memoryStream.Seek(0, SeekOrigin.Begin);
            return Results.File(memoryStream.ToArray(), "application/zip", $"Datenbank_{startUtc.Date:yyyy-MM-dd}_{endUtc.Date:yyyy-MM-dd}.zip");
        }

#endregion

    }
}
