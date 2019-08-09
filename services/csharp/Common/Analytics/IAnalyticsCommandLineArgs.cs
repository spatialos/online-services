using CommandLine;

namespace Improbable.OnlineServices.Common.Analytics
{
    public static class AnalyticsCommandLineArgsConstants
    {
        public const string EndpointName = "analytics.endpoint";
        public const string AllowInsecureEndpointName = "analytics.allow-insecure-endpoint";
        public const string ConfigPathName = "analytics.config-file-path";
        public const string GcpKeyPathName = "analytics.gcp-key-path";
        public const string EnvironmentName = "analytics.environment";
    }

    public interface IAnalyticsCommandLineArgs
    {
        [Option(AnalyticsCommandLineArgsConstants.EndpointName, HelpText = "Endpoint for analytics to be sent to. If not provided, then analytics " +
                                         "are disabled")]
        string Endpoint { get; set; }

        [Option(AnalyticsCommandLineArgsConstants.AllowInsecureEndpointName, Default = false, HelpText = "If set, allows http URLs for the endpoint")]
        bool AllowInsecureEndpoints { get; set; }

        [Option(AnalyticsCommandLineArgsConstants.ConfigPathName, HelpText = "Set the path for the analytics configuration file. If not set, default " +
                                           "values are assumed for all analytics events.")]
        string ConfigPath { get; set; }

        [Option(AnalyticsCommandLineArgsConstants.GcpKeyPathName, HelpText = "The path from which to load the GCP key for analytics reporting.")]
        string GcpKeyPath { get; set; }

        [Option(AnalyticsCommandLineArgsConstants.EnvironmentName,
            HelpText = "Must be one of: testing, staging, production, live. Allows endpoint to route " +
                       "analytics from different environments to different storage buckets.")]
        string Environment { get; set; }
    }
}
