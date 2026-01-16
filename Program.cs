using Gemini.Db;
using Gemini.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;

Gemini.Db.Db db = new(); //Datenbanken initialisieren
Gemini.Db.Db.InitiateDbWriting();

// 1. Native AOT Vorbereitung: CreateSlimBuilder verwenden
var builder = WebApplication.CreateSlimBuilder(args);

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
        options.Cookie.Name = "MyAuthCookie";
        options.Cookie.HttpOnly = true; // Wichtig gegen XSS
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Nur über HTTPS senden
        options.Cookie.IsEssential = true; // Für GDPR/DSGVO-Konformität //nicht notwendig, wenn keine Einwilligungspflicht besteht
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.LoginPath = "/";
        
        // Bei API Calls wollen wir keinen Redirect auf eine Login-Seite bei 401
        //options.Events.OnRedirectToLogin = context =>
        //{
        //    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        //    return Task.CompletedTask;
        //};
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
        "http://localhost:3000", 
        "http://127.0.0.1:5500",
        "https://localhost:443"        
        ) // Deine Client-URL explizit nennen!
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()// <-- Zwingend erforderlich für Cookies
        );    
});

builder.Services.AddAntiforgery();

builder.Services.AddScoped<Db>();
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

while (!Endpoints.PleaseStop)
{
    logger.LogTrace("Webserver neu gestartet.");
    app.Lifetime.ApplicationStopping.Register(() =>
    {
        Endpoints.cancelTokenSource.Cancel();
    });
    app.Run();    
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

