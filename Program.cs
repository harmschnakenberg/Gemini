using Gemini.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using static Gemini.Db.Db;


#region Datenbank aufräumen und vorbereiten

VaccumAllDatabases(); //Datenbanken aufräumen

InitiateDbWriting(); //Daten mit Log-Flag in DB schreiben

DbLogPurge(); //DbLog begrenzen

#endregion

var builder = WebApplication.CreateSlimBuilder(args); // Native AOT: CreateSlimBuilder verwenden

#region https

builder.WebHost.UseKestrelHttpsConfiguration();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Falls Nginx auf demselben Server läuft, reicht oft die Standardeinstellung.
    // Bei externen Proxys müssen Sie KnownProxies oder KnownNetworks leeren:
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

#endregion

#region JSON für native AOT vorbereiten

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default));

#endregion

#region Authentifizierung

// 2. Authentication (Cookies) hinzufügen
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "KreuAuthCookie";
        options.Cookie.HttpOnly = true; // Wichtig gegen XSS
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Nur über HTTPS senden        
        options.Cookie.SameSite = SameSiteMode.Strict;
        //options.Cookie.SameSite = SameSiteMode.None;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60); //Ticket Lifetime
        options.Cookie.MaxAge = options.ExpireTimeSpan; // Cookie Lifetime
        options.LoginPath = "/";
        options.AccessDeniedPath = "/"; //Test
        options.Events.OnRedirectToLogin = context =>
        {
            var newRedirectUri = context.RedirectUri + (context.RedirectUri.Contains('?') ? "&" : "?") + "auth=failed";
            context.Response.Redirect(newRedirectUri);
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

builder.Services.AddAuthorization();

// CORS aktivieren, damit der Browser (wenn er auf einem anderen Port läuft) zugreifen darf
// AllowCredentials ist notwendig für Cookies über verschiedene Ports/Domains!
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowed = ApiSettings.AllowedOrigins;// Client-URL explizit nennen!
        if (false && !builder.Environment.IsDevelopment())
        {
            // Produktion: nur HTTPS-Origins akzeptieren
            allowed = allowed.Where(u => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        policy.WithOrigins(allowed)
            //.AllowAnyOrigin() //mit https nicht möglich
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // <-- Zwingend erforderlich für Cookies
    });
});

builder.Services.AddAntiforgery();
//// Antiforgery: Cookie ebenfalls auf SameSite=None setzen und Headername festlegen
//builder.Services.AddAntiforgery(options =>
//{
//    options.Cookie.Name = "XSRF-TOKEN";
//    options.Cookie.HttpOnly = true; // Token-Cookie muss durch JS lesbar sein, falls Client es liest
//    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
//    options.Cookie.SameSite = SameSiteMode.None; // <-- passend zu Auth-Cookie
//    options.HeaderName = "X-CSRF-TOKEN";
//});

builder.Services.AddScoped<Gemini.Db.Db>();
#endregion


var app = builder.Build();
ILogger logger = app.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Die Anwendung wurde gestartet!");

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
}); // Muss GANZ OBEN in der Pipeline stehen, vor Auth oder HSTS!

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    //app.UseHttpsRedirection(); //war auskommentiert?
}

app.UseForwardedHeaders();
app.UseCors("AllowFrontend");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseMiddleware<GlobalAntiforgeryMiddleware>(); // Globales CSRF-Middleware hinzufügen
app.UseStatusCodePages(async context =>
{
    if (context.HttpContext.Response.StatusCode == 404)
    {
        await context.HttpContext.Response.WriteAsync("<h1>404 - Seite nicht gefunden</h1><a href='/'>Hauptmenü</a>");
    }

    if (context.HttpContext.Response.StatusCode == 404)
    {
        await context.HttpContext.Response.WriteAsync("<h1>403 - Zugriff verweigert</h1><a href='/'>Hauptmenü</a>");
    }
});
app.UseWebSockets();
app.UseMiddleware<WebSocketMiddleware>();
app.MapEndpoints();
app.MapGet("/restart", () => { app.Lifetime.StopApplication(); });
app.Map("/db/clean", () => { VaccumAllDatabases(); });
app.Lifetime.ApplicationStopping.Register(() =>
{
    // Signalisieren, damit andere Services (z.B. Poller) herunterfahren können
    Endpoints.cancelTokenSource.Cancel();

    try
    {
        // Blockierender Flush: bis zu 5 Sekunden warten, damit gepufferte DB‑Writes noch ausgeführt werden
        StopBackgroundWriterAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
    }
    catch (Exception ex) { logger.LogError(ex, "Fehler beim Flushen des DB-Write-Queues beim Herunterfahren."); }
});

while (!Endpoints.PleaseStop)
{
    Gemini.Db.Db.DbLogInfo("Webserver gestartet.");
    logger.LogInformation("Webserver neu gestartet.");

    app.Run();

    Gemini.Db.Db.DbLogInfo("Webserver beendet.");
    logger.LogInformation("Webserver beendet.");
}

//# Direkt Vulnerable-Pakete für die Lösung prüfen
//dotnet list "G:\VisualStudio\Projekte\ASP.NETCore_SinalR\Gemini2\Gemini.sln" package --vulnerable

static class ApiSettings
{
    internal static string[] AllowedOrigins { get; } = [.. Gemini.DynContent.HtmlHelper.GetIPV4().Select(ip => $"https://{ip}")];
    //internal static string[] AllowedOrigins { get; } = [
    //    "https://harm.local",
    //    "https://kreuwebapp.local",
    //    "http://127.0.0.1",
    //    "https://localhost",
    //    "https://192.168.160.235",
    //];
}