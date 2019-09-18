using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Improbable.OnlineServices.Common.Analytics.Config;
using Improbable.OnlineServices.Common.Analytics.ExceptionHandlers;
using Newtonsoft.Json;

namespace Improbable.OnlineServices.Common.Analytics
{
    public class AnalyticsSender : IAnalyticsSender
    {
        private struct QueuedRequest
        {
            public readonly Uri Uri;
            public readonly string Content;

            public QueuedRequest(Uri uri, string content)
            {
                Uri = uri;
                Content = content;
            }
        }

        internal const string DefaultEventCategory = "cold";

        private readonly Uri _endpoint;
        private readonly AnalyticsConfig _config;
        private readonly AnalyticsEnvironment _environment;
        private readonly CancellationTokenSource _timedDispatchCancelTokenSrc = new CancellationTokenSource();
        private readonly string _sessionId = Guid.NewGuid().ToString();
        private readonly string _gcpKey;
        private readonly string _eventSource;
        private readonly int _maxEventQueueSize;
        private readonly HttpClient _httpClient;
        private readonly IDispatchExceptionStrategy _dispatchExceptionStrategy;

        private readonly ConcurrentQueue<QueuedRequest> _queuedRequests =
            new ConcurrentQueue<QueuedRequest>();

        private long _eventId;

        private string CanonicalEnvironment => _environment.ToString().ToLower();
        private bool IsQueueFull => _queuedRequests.Count >= _maxEventQueueSize;


        internal AnalyticsSender(Uri endpoint, AnalyticsConfig config, AnalyticsEnvironment environment, string gcpKey,
            string eventSource, TimeSpan maxEventQueueDelta, int maxEventQueueSize,
            IDispatchExceptionStrategy dispatchExceptionStrategy, HttpClient httpClient)
        {
            _endpoint = endpoint;
            _config = config;
            _environment = environment;
            _gcpKey = gcpKey;
            _eventSource = eventSource;
            _maxEventQueueSize = maxEventQueueSize;
            _dispatchExceptionStrategy = dispatchExceptionStrategy;
            _httpClient = httpClient;

            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    await DispatchEventQueue();
                    await Task.Delay(maxEventQueueDelta, _timedDispatchCancelTokenSrc.Token);
                }
            }, _timedDispatchCancelTokenSrc.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        /// <summary>
        /// Queues an event, leaving any dispatching work to be done in the background rather than synchronously.
        /// </summary>
        public void Send<T>(string eventClass, string eventType, Dictionary<string, T> eventAttributes, string playerId = null)
        {
            // If the queue is to be dispatched, we leave it processing in the background instead of waiting for it
            var _ = SendAsync(eventClass, eventType, eventAttributes, playerId);
        }

        /// <summary>
        /// Queues an event for sending. If the queue was full, returns a task corresponding to sending the data to
        /// the endpoint.
        /// </summary>
        public async Task SendAsync<T>(string eventClass, string eventType, Dictionary<string, T> eventAttributes, string playerId = null)
        {
            if (!_config.IsEnabled(eventClass, eventType)) return;
            // Get previous event ID after an atomic increment
            var eventId = Interlocked.Increment(ref _eventId) - 1;

            var postParams = PostParams(eventClass, eventType, eventAttributes, eventId, playerId);
            var uri = RequestUri(eventClass, eventType);

            _queuedRequests.Enqueue(new QueuedRequest(uri, JsonConvert.SerializeObject(postParams)));

            if (IsQueueFull)
            {
                await DispatchEventQueue();
            }
        }

        private Dictionary<string, string> PostParams<T>(string eventClass, string eventType,
            Dictionary<string, T> eventAttributes, long eventId, string playerId = null)
        {
            IDictionary<string, string> eventDict = new Dictionary<string, string>
            {
                { "eventEnvironment", CanonicalEnvironment },
                { "eventIndex", eventId.ToString() },
                { "eventSource", _eventSource },
                { "sessionId", _sessionId },
                { "eventClass", eventClass },
                { "eventType", eventType },
                // TODO: Add versioning ability & resolve matching TODO in relevant unit tests
                { "versionId", "0.2.0" },
                { "eventTimestamp", (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / (decimal) 1000).ToString("F") },
                { "eventAttributes", JsonConvert.SerializeObject(eventAttributes) },
            };
            if (!String.IsNullOrEmpty(playerId)) {
                eventDict.Add(new KeyValuePair<string, string>("playerId", playerId));
            }
            return (Dictionary<string, string>) eventDict;
        }

        private Uri RequestUri(string eventClass, string eventType)
        {
            return new UriBuilder(_endpoint)
            {
                Query = DictionaryToQueryString(new Dictionary<string, string>
                {
                    { "key", _gcpKey },
                    { "analytics_environment", CanonicalEnvironment },
                    { "event_category", _config.GetCategory(eventClass, eventType) },
                    { "session_id", _sessionId }
                })
            }.Uri;
        }

        private async Task DispatchEventQueue()
        {
            Dictionary<Uri, List<string>> uriMap = new Dictionary<Uri, List<string>>();

            while (_queuedRequests.TryDequeue(out QueuedRequest request))
            {
                if (!uriMap.ContainsKey(request.Uri))
                {
                    uriMap[request.Uri] = new List<string>();
                }

                uriMap[request.Uri].Add(request.Content);
            }

            try
            {
                var enumerable = uriMap.Select(kvp =>
                    _httpClient.PostAsync(kvp.Key,
                        new StringContent($"[{string.Join(",", kvp.Value)}]")
                    )
                );
                await Task.WhenAll(enumerable.ToArray<Task>());
            }
            catch (HttpRequestException e)
            {
                _dispatchExceptionStrategy.ProcessException(e);
            }
        }

        private static string DictionaryToQueryString(Dictionary<string, string> urlParams)
        {
            return string.Join("&", urlParams.Select(
                p => $"{HttpUtility.UrlEncode(p.Key)}={HttpUtility.UrlEncode(p.Value)}"
            ));
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _timedDispatchCancelTokenSrc.Cancel();
        }
    }
}
