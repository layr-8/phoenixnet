using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace  PhoenixNet.Models
{
    public class PresenceMeta
    {
        [JsonPropertyName("phx_ref")]
        public string PhxRef { get; set; }
    }
}