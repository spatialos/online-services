using System.Collections.Generic;
using System.Threading.Tasks;

namespace Improbable.OnlineServices.Common.Analytics
{
    /// <summary>
    /// A black hole for analytics messages which does nothing; used as a default implementation for when analytics
    /// is disabled.
    /// </summary>
    public class NullAnalyticsSender : IAnalyticsSender
    {
        public Task Send(string eventClass, string eventType, Dictionary<string, string> eventAttributes)
        {
            // This method deliberately left blank
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            // This method deliberately left blank
        }
    }
}
