using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Improbable.OnlineServices.Common.Analytics
{
    /// <summary>
    /// An interface for analytics senders, used to facilitate the NullAnalyticsSender as a black hole for analytics.
    /// Normal usage should be through the Build method in AnalyticsSender.
    /// </summary>
    public interface IAnalyticsSender : IDisposable
    {
        /// <summary>
        /// Dispatch an analytics event to an endpoint, if one has been specified
        /// </summary>
        /// <param name="eventClass">A high level identifier for the event, e.g. deployment or gateway</param>
        /// <param name="eventType">A more specific identifier for the event, e.g. `join`</param>
        /// <param name="eventAttributes">A dictionary of k/v data about the event, e.g. queue duration</param>
        /// /// <param name="playerId">Optional, the ID of the player if available</param>
        void Send<T>(string eventClass, string eventType, Dictionary<string, T> eventAttributes, string playerId = null);

        Task SendAsync<T>(string eventClass, string eventType, Dictionary<string, T> eventAttributes, string playerId = null);
    }
}
