// Klassen für Datenaustausch und JSON Source Generation

// Das Datenmodell für den Austausch
using Gemini.Models;
using System.Text.Json.Serialization;

namespace Gemini.Models
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="UserName"></param>
    /// <param name="UserToken"></param>
    public record LoginRequest(string UserName, string UserToken);
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="Token">The CSRF token used for request validation.</param>
    public record CsrfTokenResponse(string Token);

    /// <summary>
    /// Represents a message intended to alert the user, including its type and display text.
    /// </summary>
    /// <param name="Type">The type of the alert message, such as "error", "warning", or "info". Determines how the message should be
    /// interpreted or displayed.</param>
    /// <param name="Text">The text content of the alert message to be shown to the user.</param>
    public record AlertMessage(string Type, string Text);

    /// <summary>
    /// Represents a JSON tag that associates a name with a value and a timestamp.
    /// </summary>
    /// <param name="N">The name of the tag. This value identifies the tag within the JSON structure.</param>
    /// <param name="V">The value associated with the tag. Can be any object or null if the tag has no value.</param>
    /// <param name="T">The timestamp indicating when the tag was created or last modified.</param>
    public record JsonTag(string N, object? V, DateTime T);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="tagName"></param>
    /// <param name="tagComment"></param>
    /// <param name="tagValue"></param>
    /// <param name="chartFlag"></param>
    public class Tag(string tagName, string tagComment, object? tagValue, bool chartFlag)
    {
        /// <summary>
        /// The name of the tag, which serves as an identifier for the tag within the system. This property is initialized with the provided tagName parameter and can be used to reference the tag in various operations, such as updates, retrievals, or display purposes.
        /// </summary>
        public string TagName { get; set; } = tagName;
        /// <summary>
        /// A comment or description associated with the tag, providing additional context or information about the tag's purpose, usage, or any relevant details. This property is initialized with the provided tagComment parameter and can be used to enhance the understanding of the tag for users or developers interacting with it.
        /// </summary>
        public string TagComment { get; set; } = tagComment;
        /// <summary>
        /// The value assigned to the tag, which can be of any type (object) or null if the tag has no value. This property is initialized with the provided tagValue parameter and represents the current state or data associated with the tag. It can be used for various purposes, such as storing configuration values, tracking state, or holding any relevant information that the tag is meant to represent.
        /// </summary>
        public object? TagValue { get; set; } = tagValue;
        /// <summary>
        /// A boolean flag indicating whether the tag is associated with a chart or not. This property is initialized with the provided chartFlag parameter and can be used to determine if the tag should be included in chart-related operations, such as data visualization or chart configuration. A value of true indicates that the tag is relevant for charts, while false indicates that it is not.
        /// </summary>
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
        /// <summary>
        /// The timestamp indicating when the tag was modified. This property is initialized with the provided timestamp parameter and serves as a record of the exact time when the alteration occurred. It can be used for auditing purposes, tracking changes over time, and associating modifications with specific events or user actions.
        /// </summary>
        public DateTime Timestamp { get; set; } = timestamp;
        
        /// <summary>
        /// The new value assigned to the tag after the modification. This property is initialized with the provided tagValue parameter and represents the current state of the tag following the alteration. It can be used to understand what the tag's value is after the change and can be compared with the OldValue property to see what was modified.
        /// </summary>
        public object? NewValue { get; set; } = tagValue;
        /// <summary>
        /// The previous value of the tag before the modification. This property is initialized with the provided oldValue parameter and represents the state of the tag prior to the alteration. It can be null if the tag was newly created or had no prior value. This property is useful for auditing changes, understanding what was modified, and potentially reverting changes if necessary.
        /// </summary>
        public object? OldValue { get; set; } = oldValue;
        /// <summary>
        /// The identifier of the user who performed the tag modification. This property is initialized with the provided user parameter and serves to associate the change with a specific user. It can be used for accountability, tracking user actions, and understanding who made specific changes to tags within the system.
        /// </summary>
        public string User { get; set; } = user;
    }

    
    /// <summary>
    /// Represents a navigation link item for a menu, including its identifier, display name, and target URL.
    /// </summary>
    /// <param name="id">The unique identifier for the menu link.</param>
    /// <param name="name">The display name of the menu link as shown to users.</param>
    /// <param name="link">The URL or path that the menu link points to.</param>
    public class MenuLink(int id, string name, string link)
    {
        /// <summary>
        /// The unique identifier for the menu link. This property is initialized with the provided id parameter and serves as a reference for the menu item within the application's navigation structure. It can be used to identify and manage menu links programmatically, such as when generating menus dynamically or handling user interactions with specific links.
        /// </summary>
        public int Id { get; set; } = id;
        /// <summary>
        /// The display name of the menu link as shown to users. This property is initialized with the provided name parameter and represents the text that will be visible in the user interface for this menu item. It can be used to provide a clear and descriptive label for the link, enhancing user experience and navigation within the application.
        /// </summary>
        public string Name { get; set; } = name;
        /// <summary>
        /// The URL or path that the menu link points to. This property is initialized with the provided link parameter and defines the destination that users will be directed to when they click on the menu link. It can be an internal route within the application or an external URL, depending on the intended navigation behavior. This property is essential for enabling users to access different sections of the application or external resources through the menu system.
        /// </summary>
        public string Link { get; set; } = link;
    }

    


   
    


}

// Source Generator Context

[JsonSerializable(typeof(Dictionary<string, MenuLink[]>))]
[JsonSerializable(typeof(MenuLink[]))]
[JsonSerializable(typeof(SollwertFromJson[]))]
[JsonSerializable(typeof(LoginRequest))]
//[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(CsrfTokenResponse))]
[JsonSerializable(typeof(ChartConfig))]
//[JsonSerializable(typeof(ChartConfig[]))]
[JsonSerializable(typeof(Tag[]))]
//[JsonSerializable(typeof(TagCollection))]
//[JsonSerializable(typeof(TagCollection[]))]
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

