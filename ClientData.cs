
// Klassen für Datenaustausch und JSON Source Generation

// Das Datenmodell für den Austausch
using System.Text.Json.Serialization;

namespace Gemini
{

    public class ClientData
    {
        public int Counter { get; set; }
        public string Message { get; set; }
        public DateTimeOffset ServerTime { get; set; }

        // Standardkonstruktor für die Deserialisierung notwendig
        public ClientData()
        {
            Message = string.Empty; // Initialisiere Non-Nullable-Eigenschaften
        }
    }

}

    // Source Generator Context
    [JsonSerializable(typeof(Gemini.ClientData))]
    internal partial class AppJsonSerializerContext : JsonSerializerContext { }

