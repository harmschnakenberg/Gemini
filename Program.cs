using Gemini.Middleware;
using Gemini.Services;
using Gemini.Db;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Gemini.Models;

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
    ctx.Response.StatusCode = 200;
    ctx.Response.ContentType = "application/json";

    string[] tagNames = ["A01_DB10_DBW2", "A01_DB10_DBW4", "A01_DB10_DBW6"];

    JsonTag[] obj = await Db.GetDataSet(tagNames, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow);
 
    await ctx.Response.WriteAsJsonAsync(obj, AppJsonSerializerContext.Default.JsonTagArray);
    await ctx.Response.CompleteAsync();
});

app.Run();
