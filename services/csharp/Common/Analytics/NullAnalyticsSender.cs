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
        public void Send<T>(string eventClass, string eventType, Dictionary<string, T> eventAttributes, string playerId = null)
        {
            // This method intentionally left blank
        }

        public Task SendAsync<T>(string eventClass, string eventType, Dictionary<string, T> eventAttributes, string playerId = null)
        {
            // This method intentionally left blank
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            // This method intentionally left blank
        }
    }
}
