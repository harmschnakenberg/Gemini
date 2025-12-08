using Gemini.Db;
using Gemini.DynContent;
using Gemini.Middleware;
using Gemini.Models;
using System.Text;
using System.Text.Json;

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
    app.MapGet("/menu/soll", async ctx =>
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

        //Console.WriteLine($"DB Request received with query: {ctx.Request.QueryString}");


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
        JsonTag[] obj = await Db.GetDataSet2(tagNames!, start, end);

        //Console.WriteLine($"Sende {JsonSerializer.Serialize(obj, AppJsonSerializerContext.Default.JsonTagArray)}");
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
        Dictionary<string, string> allTags = await Db.GetDbTagNames(DateTime.UtcNow, 3);

        List<JsonTag> result = [];

        foreach (var tag in allTags)        
            result.Add(new JsonTag(tag.Key, tag.Value, DateTime.Now));
                       
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

    //Console.WriteLine($"/excel TagNames für Abfrage: {string.Join('|', tagNames)}");
    //string[] comments = tagsAndCommnets.Values.ToArray();

    //string[] comments = [.. tagsAndCommnets.OrderBy(t => t.Key).ToDictionary().Values];
    //Dictionary<string, string> tagNamesAndComment = await Db.GetTagNamesFromComments(comments);

    Console.WriteLine($"Interval = {interval}");

    JsonTag[] obj = await Db.GetDataSet2(tagNames!, start, end);
    MemoryStream fileStream = await Excel.CreateExcelWb((Excel.Interval)interval, tagsAndCommnets, obj);

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
});

while (true)
{
    app.Run();
    Console.WriteLine("Webserver neu gestartet.");
}

