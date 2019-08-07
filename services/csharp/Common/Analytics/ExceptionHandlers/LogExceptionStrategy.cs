using System.Net.Http;
using Serilog;

namespace Improbable.OnlineServices.Common.Analytics.ExceptionHandlers
{
    public class LogExceptionStrategy : IDispatchExceptionStrategy
    {
        private readonly ILogger _logger;

        public LogExceptionStrategy(ILogger logger)
        {
            _logger = logger;
        }

        public void ProcessException(HttpRequestException e)
        {
            _logger.Error(e, "Failed to dispatch analytics events to endpoint");
        }
    }
}
