using Gemini.Db;
using Gemini.DynContent;
using Gemini.Models;
using Gemini.Services;
using Microsoft.AspNetCore.Mvc;
using S7.Net;
using S7.Net.Types;
using System.Security.Claims;
using System.Text;

namespace Gemini.Middleware
{
    public static partial class Endpoints
    {

        #region Tags
        private static IResult GetAllTagsConfig(HttpContext ctx, ClaimsPrincipal claimsPrincipal)
        {
            bool isAdmin = claimsPrincipal.IsInRole(Role.Admin.ToString());

            return Results.Content(HtmlHelper.ListAllTags(isAdmin), "text/html", Encoding.UTF8, 200);
        }

        private static IResult TagReadFailes()
        {
            string html = HtmlHelper.TagReadFailures();
            return Results.File(Encoding.UTF8.GetBytes(html), "text/html");
        }

        private static IResult TagConfigUpdate(HttpContext ctx, ClaimsPrincipal claimsPrincipal) //, IAntiforgery antiforgery
        {
            bool isAdmin = claimsPrincipal.IsInRole(Role.Admin.ToString());
            string userName = claimsPrincipal.Identity?.Name ?? "unbekannt";

            //var headers = ctx.Request.Headers;
            //foreach (var h in headers) { Console.WriteLine($"{h.Key}\t= {h.Value}");}

            string tagName = ctx.Request.Form["tagName"].ToString() ?? string.Empty;
            string tagComm = ctx.Request.Form["tagComm"].ToString() ?? string.Empty;
            string tagChck = ctx.Request.Form["tagChck"].ToString() ?? string.Empty;
            _ = bool.TryParse(tagChck, out bool isChecked);

            Db.Db.DbLogInfo($"{userName} veranlasst Tag-Update: {tagName}: {tagComm} | Log {isChecked}");

            if (isAdmin)
            {
                Db.Db.TagUpdate(tagName, tagComm, isChecked);
                return Results.Ok();
            }
            else
                return Results.Unauthorized();
        }

        private static IResult DbQuery(HttpContext ctx,             
           [FromQuery(Name = "tagnames")] string? tagNamesStr, 
           [FromQuery(Name = "start")] string? startStr, 
           [FromQuery(Name = "end")] string? endStr, 
           [FromQuery(Name = "interval")] int interval = 0)
        {
            const int MaxTagNames = 50;
            const int MaxTagNameLength = 100;

            string[]? tagNames = [];

            if (!string.IsNullOrEmpty(tagNamesStr))
            {
                tagNames = [.. tagNamesStr.Split(',')
                    .Take(MaxTagNames)
                    .Where(t => !string.IsNullOrWhiteSpace(t) 
                    && t.Length <= MaxTagNameLength
                    && t.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')
                    )
                    .Select(t => t.Trim())];
            }

            if (tagNames.Length == 0)
                return Results.BadRequest("Keine gültigen TagNames übergeben");

            System.DateTime startUtc = System.DateTime.UtcNow.AddHours(-8);
            System.DateTime endUtc = System.DateTime.UtcNow;

            if(System.DateTime.TryParse(startStr, out System.DateTime s))
                startUtc = s.ToUniversalTime(); //lokale Zeit in UTC
              
            if(System.DateTime.TryParse(endStr, out System.DateTime e))
                endUtc = e.ToUniversalTime();

            // Sicherheitsbereich erzwingen
            const int MaxDayRange = 92;
            if ((endUtc - startUtc).TotalDays > MaxDayRange)
                return Results.BadRequest($"Maximaler Zeitraum von {MaxDayRange} Tage erlaubt");

            if (startUtc > endUtc)
                return Results.BadRequest("Start muss vor dem Ende liegen");

            // Validiere Interval
            if (interval < 0 || interval > 6)
                return Results.BadRequest("Ungültiger Intervall");

            JsonTag[] obj = Db.Db.GetDataSet(tagNames, startUtc, endUtc, (MiniExcel.Interval)interval).Result;
     
            return Results.Json(obj, AppJsonSerializerContext.Default.JsonTagArray);
        }

        private static IResult GetTagComments()
        {
            List<Tag> allTags = Db.Db.GetDbTagNames(System.DateTime.UtcNow, 3);

            List<JsonTag> result = [];

            foreach (var tag in allTags)
            {
                // Console.WriteLine($"{tag.TagName} = {tag.TagValue}");
                result.Add(new JsonTag(tag.TagName, tag.TagComment, System.DateTime.Now));
            }

            return Results.Json([.. result], AppJsonSerializerContext.Default.JsonTagArray);
        }

        private static IResult WriteTagValue(HttpContext ctx, ClaimsPrincipal user)
        {
            const int MaxTagValueLength = 10000; // 10 KB max
            const int MaxTagNameLength = 256;

            string username = user.Identity?.Name ?? "-unbekannt-";
            string tagName = ctx.Request.Form["tagName"].ToString() ?? string.Empty;
            string tagVal = ctx.Request.Form["tagVal"].ToString() ?? string.Empty;
            string oldVal = ctx.Request.Form["oldVal"].ToString() ?? string.Empty;

            // Größenlimits prüfen
            if (tagName.Length > MaxTagNameLength)
                return Results.BadRequest($"Tag-Name zu lang (max {MaxTagNameLength} Zeichen)");

            if (tagVal.Length > MaxTagValueLength)
                return Results.BadRequest($"Tag-Wert zu lang (max {MaxTagValueLength} Zeichen)");


            if (!user.IsInRole(Role.Admin.ToString()) && !user.IsInRole(Role.User.ToString()))
            {
#if DEBUG
                //Console.WriteLine($"Benutzer {user.Identity?.Name} ist [{user.Claims.FirstOrDefault()?.Value}] - keine Berechtigung {tagName} zu ändern.");
#endif
                return Results.Unauthorized();
            }
            else
            {
#if DEBUG
                Console.WriteLine($"Benutzer {user.Identity?.Name} [{user.Claims.FirstOrDefault()?.Value}] - Versucht tagName auf '{tagVal}' zu ändern.");
#endif
                Db.Db.DbLogInfo($"Tag-Änderung durch {user.Identity?.Name}: TagName={(tagName?.Length > 50 ? tagName[..50] + "..." : tagName ?? "?")}, Status=Success");
            }

                if (string.IsNullOrEmpty(tagName) || string.IsNullOrEmpty(tagVal))
                {
                    return Results.Json(new AlertMessage(Type: "error", Text: "Tagname oder Wert fehlt"), AppJsonSerializerContext.Default.AlertMessage);
                }

            int result = Db.Db.WriteTag(tagName, tagVal, oldVal, username);

            if (tagName.Contains('X'))
            {   //object as int siehe https://stackoverflow.com/a/745204/22035462
                Console.WriteLine($"\r\n {tagName} '{oldVal}' > '{tagVal}'");
                tagVal = Convert.ToInt32(tagVal) > 0 ? "☒" : "☐";
                oldVal = Convert.ToInt32(oldVal) > 0 ? "☒" : "☐";
            }

            if (result > 0)
                return Results.Json(new AlertMessage(Type: "success", Text: $"Tag [{tagName}] von [{oldVal}] auf [{tagVal}] gesetzt"), AppJsonSerializerContext.Default.AlertMessage);
            else
                return Results.Json(new AlertMessage(Type: "error", Text: $"Tag [{tagName}] konnte nicht auf Wert [{tagVal}] gesetzt werden [{result}]"), AppJsonSerializerContext.Default.AlertMessage);

        }

        private static IResult GetAlterations(HttpContext ctx)
        {      
            System.DateTime startUtc = System.DateTime.UtcNow.AddDays(-1);
            System.DateTime endUtc = System.DateTime.UtcNow;

            if (ctx.Request.Query.TryGetValue("start", out var startStr) && System.DateTime.TryParse(startStr, out System.DateTime s))            
                startUtc = s.ToUniversalTime(); 
            
            if (ctx.Request.Query.TryGetValue("end", out var endStr) && System.DateTime.TryParse(endStr, out System.DateTime e))            
                endUtc = e.ToUniversalTime();

            string filter = string.Empty;
            if (ctx.Request.Query.TryGetValue("filter", out var rawFilter))
                filter = rawFilter.ToString();

            filter = filter.ToString() ?? string.Empty;

            var aTags =  Db.Db.SelectTagAlterations(startUtc, endUtc, filter);
#if DEBUG
            Console.WriteLine($"Zeige Sollwertänderungen {startUtc.ToLocalTime()} bis {endUtc.ToLocalTime()}. Filter '%{filter}%' {aTags.Count} Änderungen gefunden.");
#endif
            string html = HtmlHelper.ListAlteredTags(aTags, startUtc, endUtc, filter) ?? "<html><h1>leer</h1></html>";

            //Console.WriteLine(html);
            
            return Results.Content(html, "text/html");
        }

        #endregion

        #region SPS
        private static IResult GetAllPlcConfig(ClaimsPrincipal claimsPrincipal)
        {
            bool isReadonly = !claimsPrincipal.IsInRole(Role.Admin.ToString());
            return Results.Content(HtmlHelper.ListAllPlcConfigs(isReadonly).Result, "text/html");
        }

        private static IResult PlcCreate(HttpContext ctx, ClaimsPrincipal user)
        {
            bool isAdmin = user.IsInRole(Role.Admin.ToString());
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


            plcName += System.DateTime.Now.Millisecond; //Name ist UNIQUE

            PlcConf plc = new(0, plcName, plcType, plcIp, plcRack, plcSlot, plcIsActive, plcComm);
            Db.Db.DbLogInfo($"Neue SPS: {plc.Name}, Ip:{plc.Ip}, Type {plcType}, Rack {plc.Rack}, Slot {plc.Slot} {(plc.IsActive ? "Aktiv" : "Pausiert")}, '{plc.Comment}' von {user.Identity?.Name} [{user.Claims?.FirstOrDefault()?.Value}]");
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
            Db.Db.DbLogInfo($"Änderung für SPS: {plc.Id}, {plc.Name}, Ip:{plc.Ip}, Type {plcType}, Rack {plc.Rack}, Slot {plc.Slot} {(plc.IsActive ? "Aktiv" : "Pausiert")}, '{plc.Comment}' von {user.Identity?.Name} [{user.Claims?.FirstOrDefault()?.Value}]");

            bool isAdmin = user.IsInRole(Role.Admin.ToString());
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

            bool isAdmin = user.IsInRole(Role.Admin.ToString());
            if (!isAdmin) //Nur Administratoren dürfen SPSen konfigurieren
                return Results.Unauthorized();

            int result = Db.Db.DeletePlc(plcId);

            if (result > 0)
            {
                Db.Db.DbLogInfo($"SPS {plcId} gelöscht von {user.Identity?.Name} [{user.Claims?.FirstOrDefault()?.Value}]");
                return Results.Json(new AlertMessage("success", $"SPS [{plcId}] gelöscht"), AppJsonSerializerContext.Default.AlertMessage);
            }
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

        #endregion

        #region Datenbank

        /// <summary>
        /// Listet alle Datenbanken auf, die in der Konfiguration definiert sind. 
        /// Nützlich für die Fehlersuche bei Verbindungsproblemen oder um zu überprüfen, ob die Datenbankverbindung korrekt eingerichtet ist.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private static IResult DbList(HttpContext context)
        {
            return Results.Content(HtmlHelper.ListAllDatabases(), "text/html");
        }

        #endregion

    }
}
