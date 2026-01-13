
// Klassen für Datenaustausch und JSON Source Generation

// Das Datenmodell für den Austausch
using Gemini.DynContent;
using Gemini.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Gemini.Models
{
    public record LoginRequest(string Username, string Password);

    public record LoginResponse(string RequestToken);

    public record CsrfTokenResponse(string Token);

    public record AlertMessage(string type, string text);
    

    public class JsonTag(string n, object? v, DateTime t)
    {        
        public string N { get; set; } = n;
        public object? V { get; set; } = v;
        public DateTime T { get; set; } = t;

    }

    public class Tag(string tagName, string tagComment, object? tagValue, bool chartFlag)
    {
        public string TagName { get; set; } = tagName;
        public string TagComment { get; set; } = tagComment;
        public object? TagValue { get; set; } = tagValue;
        public bool ChartFlag { get; set; } = chartFlag;

    }

    public record FormPost(
    [Required] DateTime Start,
    [Required] DateTime End,
    [Required, Range(0, 5)] MiniExcel.Interval Interval,
    [Required] Dictionary<string, string> TagsAndComments
    );

   
    public class MenuLink(int id, string name, string link)
    {
        public int Id { get; set; } = id;
        public string Name { get; set; } = name;
        public string Link { get; set; } = link;
    }

   
}

// Source Generator Context

[JsonSerializable(typeof(Dictionary<string, Gemini.Models.MenuLink[]>))]
[JsonSerializable(typeof(Gemini.Models.MenuLink[]))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(CsrfTokenResponse))]
[JsonSerializable(typeof(Gemini.Models.FormPost))]
[JsonSerializable(typeof(Gemini.Models.JsonTag[]))]
[JsonSerializable(typeof(AlertMessage))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(Int16))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(object))]
internal partial class AppJsonSerializerContext : JsonSerializerContext { }

