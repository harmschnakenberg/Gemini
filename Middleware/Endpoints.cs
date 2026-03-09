namespace Gemini.Middleware
{
    
    public static partial class Endpoints
    {        
        internal static bool PleaseStop = false;
        internal static CancellationTokenSource cancelTokenSource = new();

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
            app.MapPost("/tag/update", TagConfigUpdate); // Tag-Kommentar und Log-Flag aktualisieren            
            app.MapPost("/tag/write", WriteTagValue).RequireAuthorization();

            app.MapGet("/excel", GetExcelForm); // Excel-Export Formular ausliefern
            app.MapPost("/excel", ExcelDownload); // Excel-Datei generieren und ausliefern
            app.MapPost("/excel/config/create", ExcelConfCreate); // Excel-Konfiguration erstellen
            app.MapPost("/excel/config/delete", ExcelConfDelete); // Excel-Konfiguration löschen //nicht implementiert  
            app.MapGet("/excel/config/all", GetExcelConf);
   
            app.MapGet("/db", DbQuery).RequireAuthorization(); // Datenbankabfrage und Ausgabe als JSON            
            app.MapPost("/db/download", DbDownload); // Datenbank-Dateien ausliefern   
            app.MapGet("/db/list", DbList).RequireAuthorization();
          
            app.MapGet("/soll", SollMenu);
            app.MapGet("/soll/history", GetAlterations);
            app.MapGet("/soll/{id:int}", SollMenu).RequireAuthorization(); // Soll-Menü HTML aus JSON-Datei erstellen und ausliefern
            app.MapGet("/chart", StaticChart).RequireAuthorization(); // Chart HTML ausliefern (bisher statisch, ToDo: TagNames dynamisch übergeben)
            app.MapGet("/chart/{chartId:int}", DynChart).RequireAuthorization();

            app.MapGet("/log", ShowLog); // Server Log

           // app.MapGet("/cert/download", CertDownload); // SSL-Zertifikat herunterladen (für Windows, ToDo: Linux)

            app.MapGet("/exit", ServerShutdown); // Server herunterfahren
            
         
            app.MapGet("/", MainMenu).AllowAnonymous(); // Hauptmenü HTML ausliefern


        }

    }

}
