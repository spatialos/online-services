using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using CommandLine;
using Improbable.OnlineServices.Common.Analytics.Config;

namespace Improbable.OnlineServices.Common.Analytics
{
    public class AnalyticsSenderBuilder
    {
        private readonly AnalyticsEnvironment _environment;
        private readonly string _gcpKey;
        private readonly string _eventSource;
        private AnalyticsConfig _config;
        private bool _allowUnsafeEndpoints;
        private HttpClient _httpClient = new HttpClient();
        private Uri _endpoint;

        private readonly string _insecureProtocolExceptionMessage
            = $"The endpoint provided uses {{0}}, but only {Uri.UriSchemeHttps} is allowed. " +
              $"Enable insecure communication with --{AnalyticsCommandLineArgs.AllowInsecureEndpointName}.";

        public AnalyticsSenderBuilder(AnalyticsEnvironment environment, string gcpKey, string eventSource)
        {
            _environment = environment;
            _gcpKey = gcpKey;
            _eventSource = eventSource;
        }

        public IAnalyticsSender Build()
        {
            _config = _config ?? new AnalyticsConfig();

            if (_endpoint != null)
            {
                if (_endpoint.Scheme != Uri.UriSchemeHttps && !_allowUnsafeEndpoints)
                {
                    throw new ArgumentException(
                        string.Format(_insecureProtocolExceptionMessage, _endpoint.Scheme));
                }

                return new AnalyticsSender(_endpoint, _config, _environment, _gcpKey, _eventSource, _httpClient);
            }

            return new NullAnalyticsSender();
        }

        public AnalyticsSenderBuilder With(HttpClient client)
        {
            _httpClient = client;
            return this;
        }

        public AnalyticsSenderBuilder With(AnalyticsConfig config)
        {
            _config = config;
            return this;
        }

        public AnalyticsSenderBuilder WithCommandLineArgs(params string[] args)
        {
            return WithCommandLineArgs(args.ToList());
        }

        public AnalyticsSenderBuilder WithCommandLineArgs(IEnumerable<string> args)
        {
            Uri endpoint;
            Parser.Default.ParseArguments<AnalyticsCommandLineArgs>(args)
                .WithParsed(async parsedArgs =>
                {
                    if (!string.IsNullOrEmpty(parsedArgs.ConfigPath))
                    {
                        _config = await AnalyticsConfig.FromFile(parsedArgs.ConfigPath);
                    }

                    if (!string.IsNullOrEmpty(parsedArgs.Endpoint))
                    {
                        _endpoint = new Uri(parsedArgs.Endpoint);
                    }

                    _allowUnsafeEndpoints = parsedArgs.AllowInsecureEndpoints;
                });
            return this;
        }
    }
}
