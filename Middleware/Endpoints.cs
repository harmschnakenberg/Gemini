using Gemini.Db;
using Gemini.Models;
using S7.Net.Types;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gemini.Middleware
{

    public static partial class Endpoints
    {
        internal static bool PleaseStop = false;
        internal static CancellationTokenSource cancelTokenSource = new();
        internal static string ChartConfigDir { get; } = Path.Combine(Db.Db.AppFolder, "wwwroot", "html", "chart");

        public static void MapEndpoints(this IEndpointRouteBuilder app)
        {
            //app.MapGet("favicon.ico", Favicon) .AllowAnonymous();
            //app.MapGet("/js/{filename}", JavaScriptFile).AllowAnonymous(); // Statische JS-Dateien dynamisch ausliefern oder über wwwroot?
            //app.MapGet("/css/{filename}", StylesheetFile).AllowAnonymous(); // Statische CSS-Dateien dynamisch ausliefern oder über wwwroot?
            app.MapGet("/{filePath:file}", ServeStaticFile).AllowAnonymous(); // Statische JS-Dateien dynamisch ausliefern | Offenbar statisch über wwwroot?

            app.MapPost("/login", Login).AllowAnonymous();
            app.MapPost("/logout", Logout).AllowAnonymous(); // Logout Endpunkt (Nötig, da Client HttpOnly Cookies nicht löschen kann) Anonym, um auf Nummer sicher zu gehen?
            app.MapGet("/antiforgery/token", RefreshAntiForgeryToken).AllowAnonymous();

            app.MapGet("/user", SelectUsers).RequireAuthorization();
            app.MapPost("/user/create", UserCreate).RequireAuthorization();
            app.MapPost("/user/update", UserUpdate).RequireAuthorization();
            app.MapPost("/user/delete", UserDelete).RequireAuthorization();

            app.MapGet("/source", GetAllPlcConfig).RequireAuthorization();
            app.MapPost("/source/create", PlcCreate).RequireAuthorization();
            app.MapPost("/source/update", PlcUpdate).RequireAuthorization();
            app.MapPost("/source/delete", PlcDelete).RequireAuthorization();
            app.MapPost("/source/ping", PlcPing).RequireAuthorization();

            app.MapGet("/tag/all", GetAllTagsConfig).RequireAuthorization(); // Alle Tags mit Kommentaren und Log-Flags als HTML-Tabelle ausliefern
            app.MapGet("/tag/failures", TagReadFailes);
            app.MapPost("/tag/comments", GetTagComments); // Tag-Kommentare abrufen
            app.MapPost("/tag/update", TagConfigUpdate).RequireAuthorization(); // Tag-Kommentar und Log-Flag aktualisieren            
            app.MapPost("/tag/write", WriteTagValue).RequireAuthorization();

            app.MapGet("/export", GetExportForm).RequireAuthorization(); // Excel-Export Formular ausliefern
            app.MapPost("/export", ExcelDownload).RequireAuthorization(); // Excel-Datei generieren und ausliefern
            
            app.MapPost("/export/config/delete", ExcelConfDelete).RequireAuthorization(); // Excel-Konfiguration löschen //nicht implementiert  
            app.MapGet("/export/config/all", GetExportConf); // Alle Excel-Export-Konfigurationen aus Datenbank als JSON ausliefern.
                                                             // Wirklich umsetzen?  ToDo: Implementieren oder entfernen?

            app.MapGet("/db", DbQuery).RequireAuthorization(); // Datenbankabfrage und Ausgabe als JSON            
            app.MapPost("/db/download", DbDownload); // Datenbank-Dateien ausliefern   
            app.MapGet("/db/list", DbList).RequireAuthorization();

            app.MapGet("/soll", ShowSoll); //
            app.MapGet("/soll/history", GetAlterations).RequireAuthorization();
            app.MapGet("/soll/{id:int}", ShowSoll).RequireAuthorization(); // Soll-Menü HTML aus JSON-Datei erstellen und ausliefern

            app.MapGet("/bild/{id:int}", ShowBild).RequireAuthorization();

            app.MapGet("/chart", StaticChart).RequireAuthorization(); // Chart HTML ausliefern (bisher statisch, ToDo: TagNames dynamisch übergeben)
            app.MapGet("/chart/{chartId:int}", DynChart).RequireAuthorization();
            app.MapPost("/chart/config/create", ChartConfigCreate).RequireAuthorization();
            app.Map("/chart/config/allnames", ChartConfigLoadNames).RequireAuthorization();
            app.MapPost("/chart/config/{configId:int}", ChartConfigImport).RequireAuthorization();

            app.MapGet("/log", ShowLog); // Server Log

            // app.MapGet("/cert/download", CertDownload); // SSL-Zertifikat herunterladen (für Windows, ToDo: Linux)

            app.MapGet("/exit", ServerShutdown); // Server herunterfahren


            app.MapGet("/", MainMenu).AllowAnonymous(); // Hauptmenü HTML ausliefern

        }

 
    }
}
