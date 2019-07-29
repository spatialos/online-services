using System.Collections.Generic;
using System.Threading.Tasks;

namespace Improbable.OnlineServices.Common.Analytics
{
    /// <summary>
    /// An interface for analytics senders to correspond to, used to facilitate the NullAnalyticsSender acting
    ///   as a black hole for analytics.
    /// Normal usage should be through the Build method in AnalyticsSender.
    /// </summary>
    public interface IAnalyticsSender
    {
        /// <summary>
        /// Dispatch an analytics event to an endpoint, if one has been specified
        /// </summary>
        /// <param name="eventClass">A high level identifier for the event, e.g. deployment or gateway</param>
        /// <param name="eventType">A more specific identifier for the event, e.g. `join`</param>
        /// <param name="eventAttributes">A dictionary of k/v data about the event, e.g. user ID or queue duration</param>
        Task Send(string eventClass, string eventType, Dictionary<string, string> eventAttributes);
    }
}
