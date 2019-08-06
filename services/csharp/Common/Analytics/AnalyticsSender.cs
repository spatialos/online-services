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

        public async Task Send(string eventClass, string eventType, Dictionary<string, string> eventAttributes)
        {
            // Get previous event ID after an atomic increment
            var eventId = Interlocked.Increment(ref _eventId) - 1;

            var postParams = PostParams(eventClass, eventType, eventAttributes, eventId);
            var uri = RequestUri(eventClass, eventType);

            _queuedRequests.Enqueue(new QueuedRequest(uri, JsonConvert.SerializeObject(postParams)));

            if (IsQueueFull)
            {
                await DispatchEventQueue();
            }
        }

        private Dictionary<string, string> PostParams(string eventClass, string eventType,
            Dictionary<string, string> eventAttributes, long eventId)
        {
            return new Dictionary<string, string>
            {
                { "eventEnvironment", CanonicalEnvironment },
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
                var enumerable = uriMap.Select(
                    kvp => _httpClient.PostAsync(kvp.Key, new StringContent(string.Join("\n", kvp.Value))));
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
