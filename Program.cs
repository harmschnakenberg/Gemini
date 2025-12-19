using Gemini.Db;
using Gemini.DynContent;
using Gemini.Middleware;
using Gemini.Models;
using Gemini.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;


bool pleaseStop = false;


Gemini.Db.Db db = new(); //Datenbanken initialisieren

// 0. Datenbank-Schreibvorgang initialisieren
Gemini.Db.Db.InitiateDbWriting();

// 1. Native AOT Vorbereitung: CreateSlimBuilder verwenden
var builder = WebApplication.CreateSlimBuilder(args);

#region Authentifizierung

var key = Encoding.UTF8.GetBytes("Dein_Super_Geheimer_Schluessel_2025_!");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false
        };
        // WICHTIG: Token aus dem Cookie lesen
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context => {
                context.Token = context.Request.Cookies["X-Access-Token"];
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default));

#endregion

var app = builder.Build();
app.UseStaticFiles();
app.UseWebSockets();
app.UseMiddleware<WebSocketMiddleware>();



app.MapPost("/login", async (JsonTag user, HttpContext ctx) =>
{    
    if (user.N == "testuser" && user?.V?.ToString() == "password123")
    {
        Console.WriteLine($"Login mit {user.N}, {user.V}");

        var token = JwtExtensions.GenerateJwt(user.N, key);

        // JWT als HttpOnly Cookie setzen
        ctx.Response.Cookies.Append("X-Access-Token", token, new CookieOptions
        {
            HttpOnly = true,
            //Secure = true,      // Nur über HTTPS
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddHours(1)
        });

        return Results.Ok();
        //ctx.Response.StatusCode = 200;
        //ctx.Response.ContentType = "text/html";
        //var file = File.ReadAllText(link, Encoding.UTF8);
        //await ctx.Response.WriteAsync(file);
        //await ctx.Response.CompleteAsync();
    }
    return Results.Unauthorized();
});

app.MapPost("/logout", (HttpContext context) =>
{
    context.Response.Cookies.Delete("X-Access-Token");
    return Results.Ok(new { message = "Ausgeloggt" });
}).AllowAnonymous();

// 4. Routen festlegen
app.MapGet("/menu/soll/{id:int}", async (int id, HttpContext ctx) =>
    {
        string json;
        using (TextReader reader = new StreamReader("wwwroot/js/sollmenu.json"))
        {
            json = await reader.ReadToEndAsync();
        }
        ;

        //Console.WriteLine(json);

        var menuTree = JsonSerializer.Deserialize(json, AppJsonSerializerContext.Default.DictionaryStringMenuLinkArray);

        if (menuTree == null)
            return;

        MenuLink? menuLink = menuTree.First().Value?.Where(i => i.Id == id).FirstOrDefault();
        string link = $"wwwroot/html/soll/{menuLink?.Link ?? string.Empty}";
        if (id == 0 || string.IsNullOrEmpty(menuLink?.Link))
            link = "wwwroot/html/menu.html";

        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "text/html";
        var file = File.ReadAllText(link, Encoding.UTF8);
        await ctx.Response.WriteAsync(file);
        await ctx.Response.CompleteAsync();
    });

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

