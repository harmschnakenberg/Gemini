using Gemini.Db;
using Gemini.DynContent;
using Gemini.Middleware;
using Gemini.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;


bool pleaseStop = false;

Gemini.Db.Db db = new();//Datenbanken initialisieren

// 0. Datenbank-Schreibvorgang initialisieren
Gemini.Db.Db.InitiateDbWriting();

// 1. Native AOT Vorbereitung: CreateSlimBuilder verwenden
var builder = WebApplication.CreateSlimBuilder(args);


// --- JWT Konfiguration aus appsettings.json ---
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
var jwtKey = builder.Configuration["Jwt:Key"];

// Identity Password Hasher Dienst registrieren
// Wir verwenden den Standard AspNetUsers Hasher für Einfachheit
builder.Services.AddSingleton<IPasswordHasher<CurrentUser>>(new PasswordHasher<CurrentUser>());

// Füge Autorisierungsdienste hinzu // Quelle: https://www.google.com/search?q=miniapi+authentifizierung+beispiel+code&client=firefox-b-d&hs=Goc9&sca_esv=b338e4d9bf6df8e5&sxsrf=AE3TifM-VSwGoD1MYW_zfu1Q8D7ZFUxp6w%3A1765467166999&udm=50&fbs=AIIjpHw2KGh6wpocn18KLjPMw8n5Yp8-1M0n6BD6JoVBP_K3fXXvA3S3XGyupmJLMg20um-mJAeO36stiqcDeSp1syInqJqhSijxtY18VJnNswqZEIqIPXL38MAteWnp4wS6uPmuMpOhUlhdP9rbJwptoX38hedzCJMh4q4oNw2kfdRn5MHw26aduF_c8rKmrLVGeF2Q5T_7&aep=1&ntc=1&sa=X&ved=2ahUKEwiT1Ofa7bWRAxUQ9LsIHQtBBt0Q2J8OegQIDBAE&biw=1869&bih=1085&dpr=1&mtid=JeQ6afzZLpOA9u8PsJKNcQ&mstk=AUtExfB0xAbacpIVnlwi68hEzi2zTqYpv8vRz8y-ougmCMOYCIy9Am-9dq22iU29oP5YOs89Av3CcXqQfFTsxBn4VEXEF_RIRG-tHhTCqcm8KzhGENfqgnqqxxPGqo8sUHD7peOiw0fG2Egac997pdz_HMiwGYRa4BpX9BX21n4XpatA8vnF2VQN15aLXsDHWKrqsUYpp9pgyNSPS3YwEqbH7-vy0KNVSz15jESDmluGbKtJGpN3exb5YQMrX6gv6VHhyXRJ10EvId6t9PgOFN_ZdSDBplo888Sdl8r2h7s4xg0KsUrKb01Re5BGokpn5zrv4OQc_BiRPxhcEA&csuir=1
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey ?? new Guid().ToString())) //Wenn jwTKey ungültig/NULL einen Zufallswert hinterlegen
        };
    });
builder.Services.AddAuthorization();

//// Hosted Service für sauberes Herunterfahren registrieren
//builder.Services.AddHostedService<ShutdownService>();

// 2. Native AOT Vorbereitung: Json Serializer Context registrieren
builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Fügt den Quell-generierten Serialisierungskontext hinzu
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});



var app = builder.Build();
//app.UseAuthentication();
//app.UseAuthorization();

// 3. WebSocket Middleware aktivieren
app.UseStaticFiles();
app.UseWebSockets();
app.UseMiddleware<WebSocketMiddleware>();


// Login-Endpunkt aktualisiert
app.MapPost("/login", (UserCredentials credentials) =>
{
Console.WriteLine($"Login-Anfrage von {credentials.Username} {credentials.Password}");

//PasswordHasherAOT.GenerateHashAndSalt(credentials.Password);

var storedSaltHash = "VGvQLgakL+zYSbv1EFundg==.IoCdvQPXQoJxUqGVQMsASdZjHLIE7d8U9pwKqlkcq6E="; // Salt.Hash

// Passwort überprüfen mit AOT-freundlicher Methode
if (PasswordHasher.VerifyPassword(credentials.Password, storedSaltHash))
{
    // Token Generierungscode (unverändert)
    var tokenHandler = new JwtSecurityTokenHandler();
    var key = Encoding.UTF8.GetBytes(jwtKey ?? new Guid().ToString()); //
    var tokenDescriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity([new Claim(ClaimTypes.Name, credentials.Username)]),
        Expires = DateTime.UtcNow.AddMinutes(15),
        Issuer = jwtIssuer,
        Audience = jwtAudience,
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
    };
    var token = tokenHandler.CreateToken(tokenDescriptor);
    var tokenString = tokenHandler.WriteToken(token);
    var j = new JsonTag(credentials.Username, tokenString, DateTime.Now);
    //Console.WriteLine($"ausgegebener Token für {j.N}: {j.V}" );
        return Results.Json(j, AppJsonSerializerContext.Default.JsonTag);
    }

    return Results.Unauthorized();
});

// 4. Routen festlegen
app.MapGet("/menu/soll/{id:int}", async (int id, HttpContext ctx) =>
    {
        string json;
        using (TextReader reader = new StreamReader("wwwroot/js/sollmenu.json"))
        {
            json = await reader.ReadToEndAsync();
        };

       // Console.WriteLine(json);

        var menuTree = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.DictionaryStringMenuLinkArray);

        if (menuTree == null)
            return;

        string key = menuTree.Keys.FirstOrDefault() ?? string.Empty;
        MenuLink? menuLink = menuTree[key]?.Where(i => i.Id == id).FirstOrDefault();
        string link = $"wwwroot/html/soll/{menuLink?.Link ?? string.Empty}";

        Console.WriteLine($"ID: {id}, link: {link}");

        if (id == 0 || string.IsNullOrEmpty(menuLink?.Link) )
            link = "wwwroot/html/menu.html";

        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "text/html";
        var file = File.ReadAllText(link, Encoding.UTF8);
        await ctx.Response.WriteAsync(file);
        await ctx.Response.CompleteAsync();
    }).AllowAnonymous();

    app.MapGet("/chart", async ctx =>
    {
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "text/html";
        var file = File.ReadAllText("wwwroot/html/chart.html", Encoding.UTF8);
        await ctx.Response.WriteAsync(file);
        await ctx.Response.CompleteAsync();
    });

    app.MapGet("/db", async ctx =>
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
        JsonTag[] obj = await Db.GetDataSet2(tagNames!, startUtc, endUtc);
#if DEBUG
        Console.WriteLine($"JsonTag Objekte zum Senden: {obj.Length}");
#endif
        // Console.WriteLine($"Sende {JsonSerializer.Serialize(obj, AppJsonSerializerContext.Default.JsonTagArray)}");
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(obj, AppJsonSerializerContext.Default.JsonTagArray);
        await ctx.Response.CompleteAsync();
    });

    app.MapGet("/all", async ctx =>
    {
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "text/html";       
        await ctx.Response.WriteAsync(await HtmlHelper.ListAllTags());
        await ctx.Response.CompleteAsync();
    });

    app.MapPost("/tagcomments", async () =>
    {
        List<Tag> allTags = await Db.GetDbTagNames(DateTime.UtcNow, 3);

        List<JsonTag> result = [];

        foreach (var tag in allTags)        
            result.Add(new JsonTag(tag.TagName, tag.TagValue, DateTime.Now));
                       
        return Results.Json([.. result], AppJsonSerializerContext.Default.JsonTagArray);
    });

    app.MapGet("/excel", async ctx =>
    {
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "text/html";
        var file = File.ReadAllText("wwwroot/html/excel.html", Encoding.UTF8);
        await ctx.Response.WriteAsync(file);
        await ctx.Response.CompleteAsync();
    });

app.MapPost("/tagupdate", async ctx =>
{
    string tagName = ctx.Request.Form["tagName"].ToString() ?? string.Empty;
    string tagComm = ctx.Request.Form["tagComm"].ToString() ?? string.Empty;
    _ = bool.TryParse(ctx.Request.Form["tagChck"].ToString(), out bool isChecked);

    Console.WriteLine($"Tag-Update: {tagName}: {tagComm} | Log {isChecked}");

    Db.TagUpdate(tagName, tagComm, isChecked);

    await ctx.Response.CompleteAsync();

});


app.MapPost("/excel", async ctx =>
{
    string jsonString = ctx.Request.Form["tags"].ToString() ?? string.Empty;
    //Console.WriteLine("/excel : " + jsonString);

    if (
    !DateTime.TryParse(ctx.Request.Form["start"], out DateTime start) ||
    !DateTime.TryParse(ctx.Request.Form["end"], out DateTime end) ||
    !int.TryParse(ctx.Request.Form["interval"], out int interval) ||
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

    Console.WriteLine($"Interval = {interval}");

    //JsonTag[] obj = await Db.GetDataSet2(tagNames!, start, end);
    //MemoryStream fileStream = await Excel.CreateExcelWb((Excel.Interval)interval, tagsAndCommnets, obj);

    JsonTag[] obj = await Db.GetDataSet2(tagNames!, start, end);
    MemoryStream fileStream = MiniExcel.DownloadExcel((MiniExcel.Interval)interval, tagsAndCommnets, obj);

    string excelFileName = $"Werte_{start:yyyyMMdd}_{end:yyyyMMdd}_{interval}_{DateTime.Now.TimeOfDay.TotalSeconds:0000}.xlsx";

    ctx.Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    ctx.Response.Headers.ContentDisposition = $"attachment; filename={excelFileName}";
    ctx.Response.ContentLength = fileStream.Length;

    await fileStream.CopyToAsync(ctx.Response.Body);
    await ctx.Response.CompleteAsync();
});

app.MapGet("/", async ctx =>
{ 
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "text/html";
        var file = File.ReadAllText("wwwroot/html/menu.html", Encoding.UTF8);
        await ctx.Response.WriteAsync(file);
        await ctx.Response.CompleteAsync();
}).AllowAnonymous();

app.MapGet("/exit", async ctx =>
{
    ctx.Response.StatusCode = 200;
    ctx.Response.ContentType = "text/html";    
    await ctx.Response.WriteAsync(HtmlHelper.ExitForm());
    await ctx.Response.CompleteAsync();

    pleaseStop = true;
});

while (!pleaseStop)
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

