using CommandLine;

namespace Improbable.OnlineServices.Common.Analytics
{
    public interface IAnalyticsCommandLineArgs
    {
        [Option(AnalyticsCommandLineArgs.EndpointName, HelpText = "Endpoint for analytics to be sent to. If not provided, then analytics " +
                                         "are disabled")]
        string Endpoint { get; set; }

        [Option(AnalyticsCommandLineArgs.AllowInsecureEndpointName, Default = false, HelpText = "If set, allows http URLs for the endpoint")]
        bool AllowInsecureEndpoints { get; set; }

        [Option(AnalyticsCommandLineArgs.ConfigPathName, HelpText = "Set the path for the analytics configuration file. If not set, default " +
                                           "values are assumed for all analytics events.")]
        string ConfigPath { get; set; }

        [Option(AnalyticsCommandLineArgs.GcpKeyPathName, HelpText = "The path from which to load the GCP key for analytics reporting.")]
        string GcpKeyPath { get; set; }

        [Option(AnalyticsCommandLineArgs.EnvironmentName,
            HelpText = "Must be one of: testing, staging, production, live. Allows endpoint to route " +
                       "analytics from different environments to different storage buckets.")]
        string Environment { get; set; }

        [Option(AnalyticsCommandLineArgs.SchemaName, HelpText = "The schema for the events that are being fired.", Default = "improbable")]
        string EventSchema { get; set; }
    }
}
