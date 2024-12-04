using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace PhoenixNet.Logging
{
    public class SerilogAdapter : ILoggerAdapter
    {
        private readonly ILogger _logger;

        public SerilogAdapter(ILogger logger)
        {
            _logger = logger;
        }

        public void Debug(string message, params object[] args) => _logger.Debug(message, args);
        public void Information(string message, params object[] args) => _logger.Information(message, args);
        public void Error(Exception ex, string message, params object[] args) => _logger.Error(ex, message, args);
    }
}
