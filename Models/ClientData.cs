
// Klassen für Datenaustausch und JSON Source Generation

// Das Datenmodell für den Austausch
using System.Text.Json.Serialization;

namespace Gemini.Models
{

    public class JsonTag(string n, object? v, DateTime t)
    {        
        public string N { get; set; } = n;
        public object? V { get; set; } = v;
        public DateTime T { get; set; } = t;

    }

}

// Source Generator Context
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Gemini.Models.JsonTag[]))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(Int16))]
[JsonSerializable(typeof(bool))]
internal partial class AppJsonSerializerContext : JsonSerializerContext { }

