using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhoenixNet.Logging
{
    
    public interface ILoggerAdapter
    {
        void Debug(string message, params object[] args);
        void Information(string message, params object[] args);
        void Error(Exception ex, string message, params object[] args);
    }

    
   
}
