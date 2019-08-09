using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using CommandLine;
using Improbable.OnlineServices.Common.Analytics.Config;
using Improbable.OnlineServices.Common.Analytics.ExceptionHandlers;

namespace Improbable.OnlineServices.Common.Analytics
{
    public class AnalyticsSenderBuilder
    {
        /// <summary>
        /// Maximum size of the event queue before all events within it are dispatched
        /// </summary>
        private int _maxQueueSize = 10;

        /// <summary>
        /// Maximum time an event should wait in the queue before being dispatched to the endpoint.
        /// May be longer if an event is added while previous events are being dispatched.
        /// </summary>
        private TimeSpan _maxQueueTime = TimeSpan.FromMilliseconds(2000);

        private AnalyticsEnvironment _environment;
        private readonly string _eventSource;
        private AnalyticsConfig _config;
        private bool _allowUnsafeEndpoints;
        private string _gcpKey;
        private HttpClient _httpClient = new HttpClient();
        private Uri _endpoint;
        private IDispatchExceptionStrategy _dispatchExceptionStrategy = new RethrowExceptionStrategy();

        private readonly string _insecureProtocolExceptionMessage
            = $"The endpoint provided uses {{0}}, but only {Uri.UriSchemeHttps} is allowed. " +
              $"Enable insecure communication with --{AnalyticsCommandLineArgs.AllowInsecureEndpointName}.";

        public AnalyticsSenderBuilder(AnalyticsEnvironment environment, string gcpKey, string eventSource)
        {
            _environment = environment;
            _gcpKey = gcpKey;
            _eventSource = eventSource;
        }

        public AnalyticsSenderBuilder(string eventSource)
        {
            _eventSource = eventSource;
        }

        public IAnalyticsSender Build()
        {
            _config = _config ?? new AnalyticsConfig();

            if (_endpoint != null && !string.IsNullOrEmpty(_gcpKey))
            {
                if (_endpoint.Scheme != Uri.UriSchemeHttps && !_allowUnsafeEndpoints)
                {
                    throw new ArgumentException(
                        string.Format(_insecureProtocolExceptionMessage, _endpoint.Scheme));
                }

                return new AnalyticsSender(_endpoint, _config, _environment, _gcpKey, _eventSource, _maxQueueTime,
                    _maxQueueSize, _dispatchExceptionStrategy, _httpClient);
            }

            return new NullAnalyticsSender();
        }

        public AnalyticsSenderBuilder With(HttpClient client)
        {
            _httpClient = client ?? throw new ArgumentNullException();
            return this;
        }

        public AnalyticsSenderBuilder With(AnalyticsConfig config)
        {
            _config = config ?? throw new ArgumentNullException();
            return this;
        }

        /// <summary>
        /// Sets the strategy for handling HTTP exceptions during analytic dispatch.
        /// </summary>
        public AnalyticsSenderBuilder With(IDispatchExceptionStrategy strategy)
        {
            _dispatchExceptionStrategy = strategy ?? throw new ArgumentNullException();
            return this;
        }

        /// <summary>
        /// Sets the maximum size the analytics event queue should reach before the queue is dispatched.
        /// </summary>
        public AnalyticsSenderBuilder WithMaxQueueSize(int maxQueueSize)
        {
            _maxQueueSize = maxQueueSize;
            return this;
        }

        /// <summary>
        /// Sets the expected duration between each dispatch of the analytics event queue.
        /// </summary>
        public AnalyticsSenderBuilder WithMaxQueueTime(TimeSpan maxQueueTime)
        {
            _maxQueueTime = maxQueueTime;
            return this;
        }

        public AnalyticsSenderBuilder WithCommandLineArgs(params string[] args)
        {
            return WithCommandLineArgs(args.ToList());
        }

        public AnalyticsSenderBuilder WithCommandLineArgs(IEnumerable<string> args)
        {
            new Parser(with => with.IgnoreUnknownArguments = true)
                .ParseArguments<AnalyticsCommandLineArgs>(args)
                .WithParsed(parsed => WithCommandLineArgs(parsed))
                .WithNotParsed(errors => throw new ArgumentException(
                    $"Failed to parse commands: {string.Join(", ", errors.Select(e => e.ToString()))}"));
            return this;
        }

        public AnalyticsSenderBuilder WithCommandLineArgs(IAnalyticsCommandLineArgs parsedArgs)
        {
            if (!string.IsNullOrEmpty(parsedArgs.ConfigPath))
            {
                _config = AnalyticsConfig.FromFile(parsedArgs.ConfigPath);
            }

            if (!string.IsNullOrEmpty(parsedArgs.Endpoint))
            {
                _endpoint = new Uri(parsedArgs.Endpoint);
            }

            if (!string.IsNullOrEmpty(parsedArgs.GcpKeyPath))
            {
                _gcpKey = File.ReadAllText(parsedArgs.GcpKeyPath).Trim();
            }

            if (!string.IsNullOrEmpty(parsedArgs.Environment))
            {
                bool result = Enum.TryParse(parsedArgs.Environment, ignoreCase: true, result: out _environment);
                if (!result)
                {
                    throw new ArgumentException($"Invalid environment {parsedArgs.Environment} given");
                }
            }

            _allowUnsafeEndpoints = parsedArgs.AllowInsecureEndpoints;
            return this;
        }
    }
}
