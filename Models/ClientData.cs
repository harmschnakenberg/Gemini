
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

    public record AlertMessage(string Type, string Text);
    

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
[JsonSerializable(typeof(FormPost))]
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

