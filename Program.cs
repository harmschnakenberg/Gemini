using Gemini.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Antiforgery;

Gemini.Db.Db db = new(); //Datenbanken initialisieren
Gemini.Db.Db.InitiateDbWriting();

// 1. Native AOT Vorbereitung: CreateSlimBuilder verwenden
var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseKestrelHttpsConfiguration();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Falls Nginx auf demselben Server läuft, reicht oft die Standardeinstellung.
    // Bei externen Proxys müssen Sie KnownProxies oder KnownNetworks leeren:
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default));

#region Authentifizierung
Endpoints.JwtSettings = new(
    Key: builder.Configuration["jwt:Key"] ?? "DeinSuperGeheimerSchluessel12345", // Mindestens 16 Zeichen für HMACSHA256
    Audience: builder.Configuration["jwt:Audience"] ?? "GeminiAudience",
    Issuer: builder.Configuration["jwt:Issuer"] ?? "GeminiIssuer"
    );


// 2. Authentifizierung & Autorisierung hinzufügen
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = Endpoints.JwtSettings.Issuer,// jwtIssuer,
        ValidAudience = Endpoints.JwtSettings.Audience, // jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Endpoints.JwtSettings.Key))
    };

    //3. Token aus Cookie extrahieren
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Wenn ein Cookie mit dem Namen existiert, nutze es als Token
            if (context.Request.Cookies.ContainsKey(Endpoints.CookieToken))
            {
                context.Token = context.Request.Cookies[Endpoints.CookieToken];                
            }
            return Task.CompletedTask;
        }
    };

});

builder.Services.ConfigureApplicationCookie(options =>
{
    // The path the user is sent to when they are not authenticated
    options.LoginPath = "/";

    //// Optional: Only redirect if it's not an API call
    //options.Events.OnRedirectToLogin = context =>
    //{
    //    if (context.Request.Path.StartsWithSegments("wwwroot/js") || context.Request.Path.StartsWithSegments("wwwroot/css"))
    //    {
    //        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    //    }
    //    else
    //    {
    //        context.Response.Redirect(context.RedirectUri);
    //    }
    //    return Task.CompletedTask;
    //};
});

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// CORS aktivieren, damit der Browser (wenn er auf einem anderen Port läuft) zugreifen darf
// AllowCredentials ist notwendig für Cookies über verschiedene Ports/Domains!
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
        //.AllowAnyOrigin()
        .WithOrigins("http://localhost:3000", "http://127.0.0.1:5500", "http://localhost:5500", "https://127.0.0.1:5500", "https://localhost:5500", "https://localhost:442", "https://localhost:443") // Deine Client-URL explizit nennen!
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()// <-- Zwingend erforderlich für Cookies
        );    
});

//builder.Services.AddAntiforgery();
builder.Services.AddAntiforgery(options =>
{
    // Set Cookie properties using CookieBuilder properties†.
    options.Cookie.Name = "KreuAntiforgeryCookie";
    options.FormFieldName = "AntiforgeryFieldname";
    options.HeaderName = "X-CSRF-TOKEN-HEADERNAME";
    options.SuppressXFrameOptionsHeader = false;
});


#endregion


var app = builder.Build();
app.UseForwardedHeaders(); // Muss GANZ OBEN in der Pipeline stehen, vor Auth oder HSTS!
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    //app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");

//AntiForgeryToken auswerten
app.Use(async (ctx, next) =>
{
    if (HttpMethods.IsPost(ctx.Request.Method))
    {
        var antiforgery = ctx.RequestServices.GetRequiredService<IAntiforgery>();
        await antiforgery.ValidateRequestAsync(ctx);
    }
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseWebSockets();
app.UseMiddleware<WebSocketMiddleware>();
app.MapEndpoints();


app.MapGet("/secure-data", (ClaimsPrincipal user) =>
{
    return Results.Ok(new { Message = $"Hallo {user.Identity?.Name}, dies kommt sicher via Cookie!", Timestamp = DateTime.Now });
})
.RequireAuthorization();


while (!Endpoints.PleaseStop)
{
    Console.WriteLine("Webserver neu gestartet.");
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

