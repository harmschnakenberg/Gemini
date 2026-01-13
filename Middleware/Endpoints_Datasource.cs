using Gemini.Db;
using Gemini.DynContent;
using Gemini.Models;
using Gemini.Services;
using S7.Net;
using System.Security.Claims;
using static System.Net.Mime.MediaTypeNames;

namespace Gemini.Middleware
{
    public static partial class Endpoints
    {

        private static IResult GetAllPlcConfig(ClaimsPrincipal claimsPrincipal)
        {
            bool isReadonly = !claimsPrincipal.IsInRole("Admin");
            return Results.Content(HtmlHelper.ListAllPlcConfigs(isReadonly).Result, "text/html");
        }

        private static async Task GetAllTagsConfig(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html";
            await ctx.Response.WriteAsync(await HtmlHelper.ListAllTags());
            await ctx.Response.CompleteAsync();
        }

        private static IResult TagConfigUpdate(HttpContext ctx, ClaimsPrincipal claimsPrincipal) //, IAntiforgery antiforgery
        {
            bool isAdmin = claimsPrincipal.IsInRole("Admin");
            string userName = claimsPrincipal.Identity?.Name ?? "unbekannt";

            //var headers = ctx.Request.Headers;
            //foreach (var h in headers) { Console.WriteLine($"{h.Key}\t= {h.Value}");}

            string tagName = ctx.Request.Form["tagName"].ToString() ?? string.Empty;
            string tagComm = ctx.Request.Form["tagComm"].ToString() ?? string.Empty;
            string tagChck = ctx.Request.Form["tagChck"].ToString() ?? string.Empty;
            _ = bool.TryParse(tagChck, out bool isChecked);

            Console.WriteLine($"{userName} veranlasst Tag-Update: {tagName}: {tagComm} | Log {isChecked}");

            if (isAdmin)
            {
                Db.Db.TagUpdate(tagName, tagComm, isChecked);
                return Results.Ok();
            }
            else
                return Results.Unauthorized();
        }


        private static async Task DbQuery(HttpContext ctx)
        {
            string[]? tagNames = [];
            DateTime startUtc = DateTime.UtcNow.AddHours(-8);
            DateTime endUtc = DateTime.UtcNow;

            //Console.WriteLine($"DB Request received with query: {ctx.Request.QueryString}");


            if (ctx.Request.Query.TryGetValue("tagnames", out var tagNamesStr))
                tagNames = tagNamesStr.ToString().Split(',');

            if (ctx.Request.Query.TryGetValue("start", out var startStr) && DateTime.TryParse(startStr, out DateTime s))
            {
                startUtc = s.ToUniversalTime(); //lokale Zeit in UTC
                                                //Console.WriteLine($"Parsed start time {startStr} to {start}");
            }

            if (ctx.Request.Query.TryGetValue("end", out var endStr) && DateTime.TryParse(endStr, out DateTime e))
            {
                endUtc = e.ToUniversalTime();
                //Console.WriteLine($"Parsed end time {endStr} to {end}");
            }

            //Console.WriteLine($"DB Request for tags: {string.Join(", ", tagNames!)} from {start} to {end}");
            JsonTag[] obj = await Db.Db.GetDataSet(tagNames!, startUtc, endUtc);
#if DEBUG
            Console.WriteLine($"JsonTag Objekte zum Senden: {obj.Length}");
#endif
            // Console.WriteLine($"Sende {JsonSerializer.Serialize(obj, AppJsonSerializerContext.Default.JsonTagArray)}");
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsJsonAsync(obj, AppJsonSerializerContext.Default.JsonTagArray);
            await ctx.Response.CompleteAsync();
        }

        private static async Task<IResult> GetTagComments()
        {
            List<Tag> allTags = await Db.Db.GetDbTagNames(DateTime.UtcNow, 3);

            List<JsonTag> result = [];

            foreach (var tag in allTags)
            {
                // Console.WriteLine($"{tag.TagName} = {tag.TagValue}");
                result.Add(new JsonTag(tag.TagName, tag.TagComment, DateTime.Now));
            }

            return Results.Json([.. result], AppJsonSerializerContext.Default.JsonTagArray);
        }


        private static IResult PlcCreate(HttpContext ctx, ClaimsPrincipal user)
        {
            bool isAdmin = user.IsInRole("Admin");
            if (!isAdmin) //Nur Administratoren dürfen SPSen konfigurieren
                return Results.Unauthorized();

            string plcName = ctx.Request.Form["plcName"].ToString() ?? string.Empty;
            string plcTypeStr = ctx.Request.Form["plcType"].ToString() ?? string.Empty;
            string plcIp = ctx.Request.Form["plcIp"].ToString() ?? string.Empty;
            string plcRackStr = ctx.Request.Form["plcRack"].ToString() ?? "0";
            string plcSlotStr = ctx.Request.Form["plcSlot"].ToString() ?? "0";
            string plcIsActiveStr = ctx.Request.Form["plcIsActive"].ToString() ?? "false";
            string plcComm = ctx.Request.Form["plcComm"].ToString() ?? string.Empty;
            CpuType plcType = Db.Db.ParseCpuType(plcTypeStr);
            _ = short.TryParse(plcRackStr, out short plcRack);
            _ = short.TryParse(plcSlotStr, out short plcSlot);
            _ = bool.TryParse(plcIsActiveStr, out bool plcIsActive);


            plcName += DateTime.Now.Millisecond; //Name ist UNIQUE

            PlcConf plc = new(0, plcName, plcType, plcIp, plcRack, plcSlot, plcIsActive, plcComm);
            Console.WriteLine($"Neue SPS: {plc.Name}, Ip:{plc.Ip}, Type {plcType}, Rack {plc.Rack}, Slot {plc.Slot} {(plc.IsActive ? "Aktiv" : "Pausiert")}, '{plc.Comment}' von {user.Identity?.Name} [{user.Claims?.FirstOrDefault()?.Value}]");
            int result = Db.Db.CreatePlc(plc); // Insert in Datenbank

            if (result > 0)
                return Results.Json(new AlertMessage("reload", $"SPS [{plcName}] erzeugt"), AppJsonSerializerContext.Default.AlertMessage);
            else
                return Results.InternalServerError();
        }

        private static IResult PlcUpdate(HttpContext ctx, ClaimsPrincipal user)
        {
            /*
                        plcId: plcId, 
                        plcName: plcName, 
                        plcType: plcType, 
                        plcIp: plcIp,
                        plcRack; plcRack,
                        plcSlot; plcSlot,
                        plcIsActive; plcIsActive,
                        plcComm; plcComm
            */

            string plcIdStr = ctx.Request.Form["plcId"].ToString() ?? "0";
            string plcName = ctx.Request.Form["plcName"].ToString() ?? string.Empty;
            string plcTypeStr = ctx.Request.Form["plcType"].ToString() ?? string.Empty;
            string plcIp = ctx.Request.Form["plcIp"].ToString() ?? string.Empty;
            string plcRackStr = ctx.Request.Form["plcRack"].ToString() ?? "0";
            string plcSlotStr = ctx.Request.Form["plcSlot"].ToString() ?? "0";
            string plcIsActiveStr = ctx.Request.Form["plcIsActive"].ToString() ?? "false";
            string plcComm = ctx.Request.Form["plcComm"].ToString() ?? string.Empty;

            _ = int.TryParse(plcIdStr, out int plcId);
            CpuType plcType = Db.Db.ParseCpuType(plcTypeStr);
            _ = short.TryParse(plcRackStr, out short plcRack);
            _ = short.TryParse(plcSlotStr, out short plcSlot);
            _ = bool.TryParse(plcIsActiveStr, out bool plcIsActive);

            PlcConf plc = new(plcId, plcName, plcType, plcIp, plcRack, plcSlot, plcIsActive, plcComm);
            Console.WriteLine($"Änderung für SPS: {plc.Id}, {plc.Name}, Ip:{plc.Ip}, Type {plcType}, Rack {plc.Rack}, Slot {plc.Slot} {(plc.IsActive ? "Aktiv" : "Pausiert")}, '{plc.Comment}' von {user.Identity?.Name} [{user.Claims?.FirstOrDefault()?.Value}]");

            bool isAdmin = user.IsInRole("Admin");
            if (!isAdmin) //Nur Administratoren dürfen SPSen konfigurieren
                return Results.Unauthorized();

            int result = Db.Db.UpdatePlc(plc); // Update in Datenbank
            //Console.WriteLine($"PlcUpdate DatenbankQuery Result = " + result);

            if (plc.IsActive) // Update in TagManager/PlcConnetionManager
                PlcTagManager.Instance.UpdatePlcConfig(plcName, plc.GetPlc());
            else
                PlcTagManager.Instance.RemovePlcConfig(plcName); //Inactive SPS entfernen

            if (result > 0)
                return Results.Json(new AlertMessage("reload", $"SPS [{plcName}] geändert"), AppJsonSerializerContext.Default.AlertMessage);
            else
                return Results.InternalServerError();
        }

        private static IResult PlcDelete(HttpContext ctx, ClaimsPrincipal user)
        {
            string plcIdStr = ctx.Request.Form["plcId"].ToString() ?? "0";

            _ = int.TryParse(plcIdStr, out int plcId);

            bool isAdmin = user.IsInRole("Admin");
            if (!isAdmin) //Nur Administratoren dürfen SPSen konfigurieren
                return Results.Unauthorized();

            int result = Db.Db.DeletePlc(plcId);

            if (result > 0)
                return Results.Json(new AlertMessage("success", $"SPS [{plcId}] gelöscht"), AppJsonSerializerContext.Default.AlertMessage);
            else
                return Results.InternalServerError();
        }

        private static IResult PlcPing(HttpContext ctx)
        {
            string ip = ctx.Request.Form["plcIp"].ToString() ?? "0";
            int port = 102;
            bool isalive = PlcConnectionManager.PingHost(ip, port);

            if (isalive)
                return Results.Json(new AlertMessage("success", $"SPS [{ip}] erreichbar"), AppJsonSerializerContext.Default.AlertMessage);
            else
                return Results.Json(new AlertMessage("warn", $"SPS [{ip}] nicht erreichbar"), AppJsonSerializerContext.Default.AlertMessage);
            throw new NotImplementedException();
        }

    }
}
