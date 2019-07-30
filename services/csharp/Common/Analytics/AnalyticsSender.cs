using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using CommandLine;
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
        private readonly Uri _endpoint;
        private readonly AnalyticsEnvironment _environment;
        private readonly string _sessionId = Guid.NewGuid().ToString();
        private readonly string _gcpKey;
        private readonly string _eventSource;
        private readonly HttpClient _httpClient;

        private long _eventId;

        public static IAnalyticsSender Build(IEnumerable<string> args, AnalyticsEnvironment environment,
            string gcpKey, string eventSource = "server", HttpClient client = null)
        {
            IAnalyticsSender sender = new NullAnalyticsSender();

            Parser.Default.ParseArguments<AnalyticsCommandLineArgs>(args)
                .WithParsed(parsedArgs =>
                {
                    if (parsedArgs.Endpoint != null)
                    {
                        sender = new AnalyticsSender(parsedArgs, environment, gcpKey, eventSource,
                            client ?? new HttpClient());
                    }
                });

            return sender;
        }

        private AnalyticsSender(AnalyticsCommandLineArgs args, AnalyticsEnvironment environment,
            string gcpKey, string eventSource, HttpClient httpClient)
        {
            _environment = environment;
            _gcpKey = gcpKey;
            _eventSource = eventSource;
            _httpClient = httpClient;
            _endpoint = new Uri(args.Endpoint);

            if (_endpoint.Scheme != Uri.UriSchemeHttps && !args.AllowInsecureEndpoints)
            {
                throw new ArgumentException(
                    $"The endpoint provided uses {_endpoint.Scheme}, but only {Uri.UriSchemeHttps} is allowed. "
                    + $"Enable insecure communication with --{AnalyticsCommandLineArgs.AllowInsecureEndpointName}.");
            }
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
                {"eventEnvironment", environment},
                {"eventIndex", eventId.ToString()},
                {"eventSource", _eventSource},
                {"eventClass", eventClass},
                {"eventType", eventType},
                {"sessionId", _sessionId},
                // TODO: Add versioning ability & resolve matching TODO in relevant unit tests
                {"buildVersion", "v0.0.0"},
                {"eventTimestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()},
                {"eventAttributes", JsonConvert.SerializeObject(eventAttributes)},
            };

            UriBuilder builder = new UriBuilder(_endpoint)
            {
                Query = DictionaryToQueryString(new Dictionary<string, string>
                {
                    {"key", _gcpKey},
                    {"analytics_environment", environment},
                    {"event_category", ""},
                    {"session_id", _sessionId}
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
