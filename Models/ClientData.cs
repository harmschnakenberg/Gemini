
// Klassen für Datenaustausch und JSON Source Generation

// Das Datenmodell für den Austausch
using Gemini.DynContent;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Gemini.Models
{

    public class JsonTag(string n, object? v, DateTime t)
    {        
        public string N { get; set; } = n;
        public object? V { get; set; } = v;
        public DateTime T { get; set; } = t;

    }

    public record FormPost(
    [Required] DateTime Start,
    [Required] DateTime End,
    [Required, Range(0, 5)] Excel.Interval Interval,
    [Required] Dictionary<string, string> TagsAndComments
);

}

// Source Generator Context
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Gemini.Models.FormPost))]
[JsonSerializable(typeof(Gemini.Models.JsonTag[]))]
[JsonSerializable(typeof(DateTime))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(Int16))]
[JsonSerializable(typeof(bool))]
internal partial class AppJsonSerializerContext : JsonSerializerContext { }

