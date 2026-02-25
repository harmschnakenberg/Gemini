using static Gemini.Db.Db;
using Gemini.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;



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
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.LoginPath = "/";
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
    options.AddPolicy("AllowFrontend",
        policy => policy
        //.AllowAnyOrigin() //mit https nicht möglich
        .WithOrigins(
        "https://harm.local",
        "https://kreuwebapp.local",
        "http://127.0.0.1",        
        "https://localhost"
        ) // Deine Client-URL explizit nennen!
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()// <-- Zwingend erforderlich für Cookies
        );    
});

builder.Services.AddAntiforgery();

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
    //app.UseHttpsRedirection();
}

app.UseForwardedHeaders();
app.UseCors("AllowFrontend");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseWebSockets();
app.UseMiddleware<WebSocketMiddleware>();
app.MapEndpoints();
app.MapGet("/restart", () => { app.Lifetime.StopApplication(); });

app.Map("/db/clean", () => { VaccumAllDatabases(); });

while (!Endpoints.PleaseStop)
{
    Gemini.Db.Db.DbLogInfo("Webserver gestartet.");
    logger.LogInformation("Webserver neu gestartet.");

    //app.Lifetime.ApplicationStopping.Register(() =>
    //{
    //    Endpoints.cancelTokenSource.Cancel();
    //});

    app.Run();

    Gemini.Db.Db.DbLogInfo("Webserver beendet.");
    logger.LogInformation("Webserver beendet.");
}



//class ShutdownService(IHostApplicationLifetime applicationLifetime) : IHostedService
//{
//    private bool pleaseStop;
//    private Task? BackgroundTask;
//    private readonly IHostApplicationLifetime applicationLifetime = applicationLifetime;

//    public Task StartAsync(CancellationToken _)
//    {
//        Console.WriteLine("Starting service");

//        BackgroundTask = Task.Run(async () =>
//        {
//            while (!pleaseStop)
//            {
//                await Task.Delay(50);
//            }

//            Console.WriteLine("Background task gracefully stopped");
//        }, _);

//        return Task.CompletedTask;
//    }

//    public async Task StopAsync(CancellationToken cancellationToken)
//    {
//        Console.WriteLine("Stopping service");

//        pleaseStop = true;
//        await BackgroundTask;

//        Console.WriteLine("Service stopped");
//    }
//}

