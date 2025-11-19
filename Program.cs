using Gemini.Middleware;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

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

app.Run();
