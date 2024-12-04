using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace  PhoenixNet.Models
{
    public class PresenceEntry
    {
        public List<PresenceMeta> Metas { get; set; } = new List<PresenceMeta>();
    }
}