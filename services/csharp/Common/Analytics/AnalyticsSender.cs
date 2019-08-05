using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Improbable.OnlineServices.Common.Analytics.Config;
using Newtonsoft.Json;

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
        private readonly CancellationToken _queueTimedDispatchCancellationToken = new CancellationToken();
        private readonly int _maxEventQueueSize;
        private readonly string _sessionId = Guid.NewGuid().ToString();
        private readonly string _gcpKey;
        private readonly string _eventSource;
        private readonly HttpClient _httpClient;

        private readonly ConcurrentQueue<(Uri uri, string content)> _queuedRequests =
            new ConcurrentQueue<(Uri, string)>();

        private long _eventId;

        internal AnalyticsSender(Uri endpoint, AnalyticsConfig config, AnalyticsEnvironment environment, string gcpKey,
            string eventSource, int maxEventQueueSize, TimeSpan maxEventQueueDelta, HttpClient httpClient)
        {
            _endpoint = endpoint;
            _config = config;
            _environment = environment;
            _gcpKey = gcpKey;
            _eventSource = eventSource;
            _maxEventQueueSize = maxEventQueueSize;
            _httpClient = httpClient;

            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    await DispatchEventQueue();
                    await Task.Delay(maxEventQueueDelta, _queueTimedDispatchCancellationToken);
                }
            }, _queueTimedDispatchCancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public async Task Send(string eventClass, string eventType, Dictionary<string, string> eventAttributes,
            bool immediateSend = false)
        {
            // Get previous event ID after an atomic increment
            var eventId = Interlocked.Increment(ref _eventId) - 1;
            string environment = _environment.ToString().ToLower();

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

            var uri = builder.Uri;

            if (immediateSend)
            {
                await SendData(uri, new StringContent(JsonConvert.SerializeObject(postParams)));
            }
            else
            {
                _queuedRequests.Enqueue((uri, JsonConvert.SerializeObject(postParams)));

                if (_queuedRequests.Count >= _maxEventQueueSize)
                {
                    await DispatchEventQueue();
                }
            }
        }

        private async Task DispatchEventQueue()
        {
            Dictionary<Uri, List<string>> uriMap = new Dictionary<Uri, List<string>>();

            // We only need to lock dequeue operations so we can ensure batches are sent together even if
            // both conditions occur at the same time - the time elapses and the queue fills at the same time.
            // In the event of a second thread trying to dispatch at the same time as another is, we just fall through
            // with the empty uriMap
            if (Monitor.TryEnter(_queuedRequests))
            {
                try
                {
                    if (_queuedRequests.Count == 0) 
                    {
                    	return;
                    }

                    while (_queuedRequests.TryDequeue(out (Uri uri, string content) request))
                    {
                        if (!uriMap.ContainsKey(request.uri)) uriMap[request.uri] = new List<string>();
                        uriMap[request.uri].Add(request.content);
                    }
                }
                finally
                {
                    Monitor.Exit(_queuedRequests);
                }
            }

            var enumerable = uriMap.Select(
                kvp => SendData(kvp.Key, new StringContent(string.Join("\n", kvp.Value)))
            );
            Task.WaitAll(enumerable.ToArray());
        }

        private Task<HttpResponseMessage> SendData(Uri uri, StringContent content)
        {
            // TODO: Process response to handle failure / verify success; possibly falling back to alternative endpoint
            return _httpClient.PostAsync(uri, content);
        }

        private static string DictionaryToQueryString(Dictionary<string, string> urlParams)
        {
            return string.Join("&", urlParams.Select(
                p => $"{HttpUtility.UrlEncode(p.Key)}={HttpUtility.UrlEncode(p.Value)}"
            ));
        }
    }
}
