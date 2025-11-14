
// Klassen für Datenaustausch und JSON Source Generation

// Das Datenmodell für den Austausch
using Gemini.Models;
using System.Text.Json.Serialization;

namespace Gemini.Models
{

    //public class ClientData(Guid guid, JsonTag[] jsonTags)
    //{
    //    public Guid ClinetId { get; set; } = guid;
    //    public JsonTag[] Tags { get; set; } = jsonTags;
    //}

    public class JsonTag(string n, object v, DateTime t)
    {
        public string N { get; set; } = n;
        public object V { get; set; } = v;
        public DateTime T { get; set; } = t;

    }

}

// Source Generator Context
[JsonSerializable(typeof(Gemini.Models.JsonTag[]))]
[JsonSerializable(typeof(double))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(bool))]
internal partial class AppJsonSerializerContext : JsonSerializerContext { }

