using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace  PhoenixNet.Models
{
    public class ReceivedResponse
    {
        public string Status { get; set; }
        public object Response { get; set; }
        public string Ref { get; set; }
    }
}
