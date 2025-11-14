using Gemini.Models;
using System.Collections.Concurrent;

namespace Gemini
{
    public class TagCollection
    {
        public static ConcurrentDictionary<string, Tag> Tags { get; } = new();

    }


    public class Tag
    {
        public Tag(string name)
        {
            Name = name;
            PlcName = name.Length >= 3 ? name[..3] : "A00";

            Refresh();
        }

        public string Name { get; set; }
        public string Comment { get; set; }
        internal string PlcName { get; private set; }
        public object Value { get; set; }

        public bool LogFlag { get; set; } = false;
        public System.DateTime TimeStamp { get; private set; }

        public bool IsExpired(System.DateTime? now = null)
        {
            var check = now ?? System.DateTime.Now;
            return !LogFlag && check > TimeStamp.AddSeconds(90);
        }
        public void Refresh()
        {
            TimeStamp = System.DateTime.UtcNow;
        }
    }
}
