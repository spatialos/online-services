using System.Collections.Generic;

namespace Improbable.OnlineServices.Common.Analytics
{
    /// <summary>
    /// A black hole for analytics messages which does nothing; used as a default implementation for when analytics
    /// is disabled.
    /// </summary>
    public class NullAnalyticsSender : IAnalyticsSender
    {
        public void Send(string eventClass, string eventType, Dictionary<string, string> eventAttributes)
        {
            // Do nothing
        }
    }
}