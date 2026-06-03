using System.Text.Json.Serialization;

namespace Gemini.Models
{
    ///// <summary>
    ///// 
    ///// </summary>
    ///// <param name="Id"></param>
    ///// <param name="Name"></param>
    ///// <param name="Author"></param>
    ///// <param name="Start"></param>
    ///// <param name="End"></param>
    ///// <param name="Interval"></param>
    ///// <param name="ChartConfig"></param>
   // public record TagCollection(int Id, string Name, string Author, DateTime Start, DateTime End, int Interval, ChartConfig ChartConfig); //Tag[] Tags

    //Todo: ChartConfig könnte man auch in der Datenbank speichern, um die Zuordnung von Tags zu Charts dynamisch zu gestalten.    
    //Die ChartConfig-Klasse definiert die Struktur für die Konfiguration von Diagrammen, einschließlich der Zuordnung von Tags zu zwei verschiedenen Diagrammen (Chart1 und Chart2).
    //ToDo: Prüfen, ob JsonTag, Tag, TagCollection und ChartConfig zusammengefasst werden können, um die Anzahl der Klassen zu reduzieren und
    //die Datenstruktur zu vereinfachen. Es könnte sinnvoll sein, eine einheitliche Klasse zu verwenden,
    //die sowohl die Tag-Informationen als auch die Chart-Konfiguration enthält, um die Handhabung der Daten zu erleichtern und Redundanzen zu vermeiden.

    /// <summary>
    /// Represents the configuration settings for a chart, including captions and the mapping of tags to two distinct
    /// charts.
    /// </summary>
    /// <remarks>Use this class to define the metadata and tag associations required to render one or two
    /// related charts. The configuration includes a main caption, an optional subcaption, and dictionaries that map tag
    /// identifiers to their display names or values for each chart. The Chart1Tags property is required and must be
    /// provided, while Chart2Tags is optional and may be null if only one chart is configured.</remarks>
    [JsonSourceGenerationOptions(WriteIndented = true)]
    public class ChartConfig
    {
        /// <summary>
        /// The unique identifier for the chart configuration, which serves as a reference for identifying and managing different chart configurations within the system. This property is of type integer and can be used to distinguish between various chart configurations when storing, retrieving, or manipulating them in a database or other data storage mechanism. The Id property is essential for maintaining the integrity and organization of chart configurations, allowing for efficient access and management of chart-related data.
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// The name of the chart configuration, which serves as a descriptive label for the chart and helps users identify and differentiate between various chart configurations. This property is of type string and can be used to provide a meaningful and user-friendly name for the chart, making it easier for users to understand the purpose or content of the chart at a glance. The Name property is important for enhancing the usability and accessibility of charts within the application, allowing users to quickly recognize and select the appropriate chart configuration based on its name.
        /// </summary>
        public string? Author { get; set; }
        /// <summary>
        /// The main caption for the chart, which serves as a title or heading that describes the content or purpose of the chart. This property is required and must be provided when creating an instance of the ChartConfig class. The Caption property is typically displayed prominently above or within the chart to provide context and information to users about what the chart represents, helping them understand the data being visualized and its significance.
        /// </summary>
        public required string Caption { get; set; }
        /// <summary>
        /// The subcaption for the chart, which serves as a secondary title or description that provides additional context or information about the chart. This property is optional and may be null if no subcaption is needed. The SubCaption can be used to offer further details, explanations, or insights related to the chart's content, helping users gain a deeper understanding of the data being visualized and its implications. It is typically displayed in a smaller font size than the main caption and can be positioned below or alongside the main caption for clarity and emphasis.
        /// </summary>
        public string? SubCaption { get; set; }
        /// <summary>
        /// A dictionary that maps tag identifiers to their corresponding display names or values for the first chart (Chart1). This property is required and must be provided when creating an instance of the ChartConfig class. The keys in the dictionary represent the unique identifiers for the tags, while the values represent the display names or values that will be used when rendering the first chart. This mapping allows for dynamic association of tags with their visual representation in the chart, enabling flexibility in how data is presented to users.
        /// </summary>
        public required Dictionary<string, string> Chart1Tags { get; set; }
        /// <summary>
        /// A dictionary that maps tag identifiers to their corresponding display names or values for the second chart (Chart2). This property is optional and may be null if only one chart is configured. The keys in the dictionary represent the unique identifiers for the tags, while the values represent the display names or values that will be used when rendering the second chart. This mapping allows for dynamic association of tags with their visual representation in the chart, enabling flexibility in how data is presented to users.
        /// </summary>
        public Dictionary<string, string>? Chart2Tags { get; set; }

        /// <summary>
        /// The start date and time for the chart configuration, indicating when the chart should begin displaying data. It can be set to a specific value to define the starting point for data visualization in the chart, allowing users to focus on a particular time range or period of interest when analyzing the data presented in the chart.
        /// </summary>
        public DateTime Start { get; set; } = DateTime.UtcNow.AddHours(-8);
        /// <summary>
        /// The end date and time for the chart configuration, indicating when the chart should stop displaying data. It can be set to a specific value to define the ending point for data visualization in the chart, allowing users to focus on a particular time range or period of interest when analyzing the data presented in the chart.
        /// </summary>
        public DateTime End { get; set; } = DateTime.UtcNow;
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
        /// <summary>
        /// A comment or description associated with the setpoint (Sollwert), providing additional context or information about the setpoint's purpose, usage, or any relevant details. This property is initialized with an empty string and can be used to enhance the understanding of the setpoint for users or developers interacting with it. It may contain user-generated comments, system-generated descriptions, or any other relevant information that helps clarify the intent or function of the setpoint within the application.
        /// </summary>
        public string Comment { get; set; } = string.Empty; // Kommentar / Beschreibung zum Sollwert
        /// <summary>
        /// A hint or additional information related to the setpoint (Sollwert), which can be displayed in the user interface to provide guidance or clarification to users. This property is initialized with an empty string and can be used to offer tips, instructions, or any other relevant information that assists users in understanding how to interact with the setpoint or what it represents. The hint may be displayed in a specific format (e.g., right-aligned in gray within a comment field) to visually differentiate it from other types of information and make it easily noticeable for users seeking guidance.
        /// </summary>
        public string Hint { get; set; } = string.Empty; // Rechtsbündig in grau im Kommentarfeld angezeigt
        /// <summary>
        /// The PLT number associated with the setpoint (Sollwert), which serves as an identifier or reference for the setpoint within the system. This property is initialized with an empty string and can be used to store and display the PLT number in the user interface, typically right-aligned in a yellow field within a comment section. The PLT number may be used for tracking, identification, or any other relevant purposes related to the setpoint's management and usage within the application.
        /// </summary>
        public string Plt { get; set; } = string.Empty; //PLT-Nummer rechtsbündig in gelbem Feld im Kommentarfeld angezeigt

        /// <summary>
        /// Represents the actual value (Istwert) associated with the setpoint, including its tag name, unit of measurement, and a flag indicating whether it is a boolean value. This struct is used to encapsulate the details of the actual value that corresponds to the setpoint, providing a structured way to access and manage this information within the application. The TagName property identifies the specific tag associated with the actual value, while the Unit property specifies the unit of measurement for that value. The IsBoolean property indicates whether the actual value is of a boolean type, which can be useful for determining how to handle or display the value in the user interface.
        /// </summary>
        /// <param name="tagname">The name of the tag associated with the actual value.</param>
        /// <param name="unit">The unit of measurement for the actual value.</param>
        public struct Ist(string tagname, string unit)
        {
            /// <summary>
            /// The name of the tag associated with the actual value (Istwert). This property is initialized with the provided tagname parameter and serves as an identifier for the specific tag that corresponds to the actual value. It can be used to reference the tag in various operations, such as retrieving the current value, updating it, or displaying it in the user interface. The TagName property is essential for linking the actual value to its corresponding tag within the system.
            /// </summary>
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
