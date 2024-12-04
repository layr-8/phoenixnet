using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace  PhoenixNet.Models
{
    public class Message
    {
        [JsonPropertyName("topic")]
        public string Topic { get; set; }

        [JsonPropertyName("event")]
        public string Event { get; set; }

        [JsonPropertyName("payload")]
        public object Payload { get; set; }

        [JsonPropertyName("ref")]
        public string Ref { get; set; }
    }
}