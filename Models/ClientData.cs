// Klassen für Datenaustausch und JSON Source Generation

// Das Datenmodell für den Austausch
using Gemini.Models;
using System.Text.Json.Serialization;

namespace Gemini.Models
{
    public record LoginRequest(string Username, string Password);

    public record LoginResponse(string RequestToken);

    public record CsrfTokenResponse(string Token);

    public record AlertMessage(string Type, string Text);

    public record JsonTag(string N, object? V, DateTime T);

    //public class JsonTag(string n, object? v, DateTime t)
    //{        
    //    public string N { get; set; } = n;
    //    public object? V { get; set; } = v;
    //    public DateTime T { get; set; } = t;

    //}


    public class Tag(string tagName, string tagComment, object? tagValue, bool chartFlag)
    {
        public string TagName { get; set; } = tagName;
        public string TagComment { get; set; } = tagComment;
        public object? TagValue { get; set; } = tagValue;    
        public bool ChartFlag { get; set; } = chartFlag;
    }

    /// <summary>
    /// Represents a record of a tag modification, including the time of change, the tag's details, the previous value,
    /// and the user responsible for the alteration.
    /// </summary>
    /// <remarks>This class is useful for auditing tag changes, tracking historical values, and associating
    /// modifications with specific users. The Tag property is initialized with the provided tag details and reflects
    /// the state after the alteration.</remarks>
    /// <param name="timestamp">The date and time when the tag was modified.</param>
    /// <param name="tagName">The name of the tag that was altered.</param>
    /// <param name="tagComment">The comment or description associated with the tag at the time of modification.</param>
    /// <param name="tagValue">The new value assigned to the tag. Can be null if the tag value is cleared or not set.</param>
    /// <param name="oldValue">The previous value of the tag before the modification. Can be null if the tag was newly created or had no prior
    /// value.</param>
    /// <param name="user">The identifier of the user who performed the tag modification.</param>
    public class TagAltered(DateTime timestamp, string tagName, string tagComment, object? tagValue, object? oldValue, string user) : Tag(tagName, tagComment, tagValue, false)
    {
        public DateTime Timestamp { get; set; } = timestamp;
        //public string TagName { get; set; } = tagName;
        //public string TagComment { get; set; } = tagComment;
        public object? NewValue { get; set; } = tagValue;        
        public object? OldValue { get; set; } = oldValue;
        public string User { get; set; } = user;
    }

    public record TagCollection(int Id, string Name, string Author, DateTime Start, DateTime End, int Interval, Tag[] Tags);

    //Todo: ChartConfig könnte man auch in der Datenbank speichern, um die Zuordnung von Tags zu Charts dynamisch zu gestalten.    
    //Die ChartConfig-Klasse definiert die Struktur für die Konfiguration von Diagrammen, einschließlich der Zuordnung von Tags zu zwei verschiedenen Diagrammen (Chart1 und Chart2).
    //ToDo: Prüfen, ob JsonTag, Tag, TagCollection und ChartConfig zusammengefasst werden können, um die Anzahl der Klassen zu reduzieren und
    //die Datenstruktur zu vereinfachen. Es könnte sinnvoll sein, eine einheitliche Klasse zu verwenden,
    //die sowohl die Tag-Informationen als auch die Chart-Konfiguration enthält, um die Handhabung der Daten zu erleichtern und Redundanzen zu vermeiden.
    internal class ChartConfig
    {
        public int Id { get; set; }
        public required string Caption { get; set; } 
        public string? SubCaption { get; set; }
        public required Dictionary<string, string> Chart1Tags { get; set; }
        public Dictionary<string, string>? Chart2Tags { get; set; }
    }


    public class MenuLink(int id, string name, string link)
    {
        public int Id { get; set; } = id;
        public string Name { get; set; } = name;
        public string Link { get; set; } = link;
    }

    /// <summary>
    /// Represents a deserialized setpoint (Sollwert) object obtained from JSON data, including associated metadata such
    /// as comments, hints, and PLT number.
    /// </summary>
    /// <remarks>This class is typically used to map setpoint information from JSON sources into strongly
    /// typed objects for further processing or display. The properties provide descriptive and contextual information
    /// related to the setpoint, such as user comments, display hints, and identification numbers. The nested structs
    /// 'Ist' and 'Soll' encapsulate additional details about actual and target values, including units and value
    /// constraints.</remarks>
    public class SollwertFromJson
    {
        public string Comment { get; set; } = string.Empty; // Kommentar / Beschreibung zum Sollwert
        public string Hint { get; set; } = string.Empty; // Rechtsbündig in grau im Kommentarfeld angezeigt
        public string Plt { get; set; } = string.Empty; //PLT-Nummer rechtsbündig in gelbem Feld im Kommentarfeld angezeigt

        public struct Ist(string tagname, string unit)
        {
            public string TagName { get; private set; } = tagname;
            public string Unit { get; private set; } = unit;
            public bool IsBoolean { get; private set; } = false;

        }

        public struct Soll(string tagname, string unit)
        {
            public string TagName { get; private set; } = tagname;
            public string Unit { get; private set; } = unit;

            public bool IsBoolean { get; private set; } = false;

            public double StepValue { get; private set; } = 0.1;
            public int MinValue { get; private set; } = -100000;
            public int MaxValue { get; private set; } = 100000;

        }


    }


   
    


}

// Source Generator Context

[JsonSerializable(typeof(Dictionary<string, MenuLink[]>))]
[JsonSerializable(typeof(MenuLink[]))]
[JsonSerializable(typeof(SollwertFromJson[]))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(CsrfTokenResponse))]
[JsonSerializable(typeof(ChartConfig))]
[JsonSerializable(typeof(Tag[]))]
[JsonSerializable(typeof(TagCollection))]
[JsonSerializable(typeof(TagCollection[]))]
[JsonSerializable(typeof(JsonTag[]))]
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

