using Gemini.Db;
using Gemini.Middleware;
using Gemini.Models;
using System.Text;

Gemini.Db.Db db = new();//Datenbanken initialisieren

// 0. Datenbank-Schreibvorgang initialisieren
Gemini.Db.Db.InitiateDbWriting();

// 1. Native AOT Vorbereitung: CreateSlimBuilder verwenden
var builder = WebApplication.CreateSlimBuilder(args);

// 2. Native AOT Vorbereitung: Json Serializer Context registrieren
builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Fügt den Quell-generierten Serialisierungskontext hinzu
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

var app = builder.Build();

// 3. WebSocket Middleware aktivieren
app.UseStaticFiles();
app.UseWebSockets();
app.UseMiddleware<WebSocketMiddleware>();

// 4. Routen festlegen
app.MapGet("/", async ctx =>
{
    ctx.Response.StatusCode = 200;
    ctx.Response.ContentType = "text/html";
    var file = File.ReadAllText("wwwroot/html/index.html", Encoding.UTF8);
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
    DateTime start = DateTime.UtcNow.AddHours(-8);
    DateTime end = DateTime.UtcNow;

    Console.WriteLine($"DB Request received with query: {ctx.Request.QueryString}");


    if (ctx.Request.Query.TryGetValue("tagnames", out var tagNamesStr))
        tagNames = tagNamesStr.ToString().Split(',');  

    if (ctx.Request.Query.TryGetValue("start", out var startStr) && DateTime.TryParse(startStr, out DateTime s))
    {        
        start = s.ToUniversalTime(); //lokale Zeit in UTC
        //Console.WriteLine($"Parsed start time {startStr} to {start}");
    }

    if (ctx.Request.Query.TryGetValue("end", out var endStr) && DateTime.TryParse(endStr, out DateTime e))
    {
        end = e.ToUniversalTime();
        //Console.WriteLine($"Parsed end time {endStr} to {end}");
    }  

    //Console.WriteLine($"DB Request for tags: {string.Join(", ", tagNames!)} from {start} to {end}");
    JsonTag[] obj = await Db.GetDataSet(tagNames!, start, end);

    //Console.WriteLine($"Sende {JsonSerializer.Serialize(obj, AppJsonSerializerContext.Default.JsonTagArray)}");
    ctx.Response.StatusCode = 200;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsJsonAsync(obj, AppJsonSerializerContext.Default.JsonTagArray);
    await ctx.Response.CompleteAsync();
});

app.Run();
