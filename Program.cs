using Gemini.Db;
using Gemini.DynContent;
using Gemini.Middleware;
using Gemini.Models;
using System.Collections.Specialized;
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
    app.MapGet("/soll", async ctx =>
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

    app.MapGet("/all", async ctx =>
    {
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "text/html";
        var file = File.ReadAllText("wwwroot/html/index.html", Encoding.UTF8);
        await ctx.Response.WriteAsync(await HtmlHelper.ListAllTags());
        await ctx.Response.CompleteAsync();
    });

    app.MapPost("/excel", async ctx =>
    {
        string responseTxt = string.Empty;

        if (!DateTime.TryParse(ctx.Request.Form["start"], out DateTime start))
            responseTxt += $"Startzeit {ctx.Request.Form["start"]} ung&uuml;ltig.\r\n";
        if (!DateTime.TryParse(ctx.Request.Form["end"], out DateTime end))
            responseTxt += $"Endzeit {ctx.Request.Form["end"]} ung&uuml;ltig.\r\n";

        Dictionary<int, string> tagsAndCommnets = new Dictionary<int, string>();

        for (int i = 0; i < ctx.Request.Form.Count; i++)
        {
            if (ctx.Request.Form.TryGetValue($"col{i}", out var tag))            
                tagsAndCommnets.Add(i, tag.ToString());                            
        }

        string[] comments = tagsAndCommnets.OrderBy(t => t.Key).ToDictionary().Values.ToArray();        
        Dictionary<string, string> tagNamesAndComment = await Db.GetTagNamesFromComments(comments);
        //responseTxt += string.Join(", ", comments);


        JsonTag[] obj = await Db.GetDataSet(tagNamesAndComment.Keys.ToArray()!, start, end);

        foreach (var item in obj)
        {
            responseTxt += $"\r\n{item.T}\t{item.N} = {item.V}";
        }


        responseTxt += $"\r\nLänge {obj.Length}";

        var excel = new Gemini.DynContent.Excel();
        excel.CreateExcelWb(start, end, tagNamesAndComment, obj);

        await ctx.Response.WriteAsync(responseTxt);
        await ctx.Response.CompleteAsync();

    });

while (true)
{
    app.Run();
    Console.WriteLine("Webserver neu gestartet.");
}

