using CommandLine;

namespace Improbable.OnlineServices.Common.Analytics
{
    public class AnalyticsCommandLineArgs
    {
        public const string EndpointName = "analytics.endpoint";
        public const string AllowInsecureEndpointName = "analytics.allow-insecure-endpoint";
        public const string ConfigPathName = "analytics.config-file-path";
        public const string GcpKeyPathName = "analytics.gcp-key-path";
        public const string EnvironmentName = "analytics.environment";

        [Option(EndpointName, HelpText = "Endpoint for analytics to be sent to. If not provided, then analytics " +
                                         "are disabled")]
        public string Endpoint { get; set; }

        [Option(AllowInsecureEndpointName, Default = false, HelpText = "If set, allows http URLs for the endpoint")]
        public bool AllowInsecureEndpoints { get; set; }

        [Option(ConfigPathName, HelpText = "Set the path for the analytics configuration file. If not set, default " +
                                           "values are assumed for all analytics events.")]
        public string ConfigPath { get; set; }

        [Option(GcpKeyPathName, HelpText = "The path from which to load the GCP key for analytics reporting.")]
        public string GcpKeyPath { get; set; }

        [Option(EnvironmentName,
            HelpText = "Must be one of: testing, staging, production, live. Allows endpoint to route " +
                       "analytics from different environments to different storage buckets.")]
        public string Environment { get; set; }
    }
}
