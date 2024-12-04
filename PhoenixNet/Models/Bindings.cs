using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace  PhoenixNet.Models
{
    public class Binding
    {
        public string Event { get; set; }
        public Action<object> Callback { get; set; }
    }
}
