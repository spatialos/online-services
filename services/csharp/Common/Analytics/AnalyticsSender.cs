using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using CommandLine;
using CSharpx;
using Improbable.OnlineServices.Common.Analytics.Config;
using Newtonsoft.Json;

[assembly: InternalsVisibleTo("Common.Test")]

namespace Improbable.OnlineServices.Common.Analytics
{
    public enum AnalyticsEnvironment
    {
        Testing,
        Development,
        Staging,
        Production,
        Live,
    }

    public class AnalyticsSender : IAnalyticsSender
    {
        internal const string DefaultEventCategory = "cold";

        private readonly Uri _endpoint;
        private readonly AnalyticsConfig _config;
        private readonly AnalyticsEnvironment _environment;
        private readonly string _sessionId = Guid.NewGuid().ToString();
        private readonly string _gcpKey;
        private readonly string _eventSource;
        private readonly HttpClient _httpClient;

        private long _eventId;

        internal AnalyticsSender(Uri endpoint, AnalyticsConfig config, AnalyticsEnvironment environment,
            string gcpKey, string eventSource, HttpClient httpClient)
        {
            _endpoint = endpoint;
            _config = config;
            _environment = environment;
            _gcpKey = gcpKey;
            _eventSource = eventSource;
            _httpClient = httpClient;
        }

        public async Task Send(string eventClass, string eventType, Dictionary<string, string> eventAttributes)
        {
            // Get previous event ID after an atomic increment
            var eventId = Interlocked.Increment(ref _eventId) - 1;
            string environment = _environment.ToString().ToLower();

            // TODO: Can the redundancy in postParams be fixed by amending the pipeline to import the URL
            //   params into JSON?
            var postParams = new Dictionary<string, string>
            {
                { "eventEnvironment", environment },
                { "eventIndex", eventId.ToString() },
                { "eventSource", _eventSource },
                { "eventClass", eventClass },
                { "eventType", eventType },
                { "sessionId", _sessionId },
                // TODO: Add versioning ability & resolve matching TODO in relevant unit tests
                { "buildVersion", "0.0.0" },
                { "eventTimestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() },
                { "eventAttributes", JsonConvert.SerializeObject(eventAttributes) },
            };

            UriBuilder builder = new UriBuilder(_endpoint)
            {
                Query = DictionaryToQueryString(new Dictionary<string, string>
                {
                    { "key", _gcpKey },
                    { "analytics_environment", environment },
                    { "event_category", _config.GetCategory(eventClass, eventType) },
                    { "session_id", _sessionId }
                })
            };

            // TODO: Process response to handle failure / verify success
            await _httpClient.PostAsync(builder.ToString(),
                new StringContent(JsonConvert.SerializeObject(postParams)));
        }

        private static string DictionaryToQueryString(Dictionary<string, string> urlParams)
        {
            return string.Join("&", urlParams.Select(
                p => $"{HttpUtility.UrlEncode(p.Key)}={HttpUtility.UrlEncode(p.Value)}"
            ));
        }
    }
}
