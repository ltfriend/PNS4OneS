using System.Collections.Generic;

namespace PNS4OneS
{
    public class Message
    {
        public string Topic { get; set; }
        public Notification Notification { get; set; }
        public Dictionary<string, string> Data { get; set; }
    }
}
