using CommandLine;
using Improbable.OnlineServices.DataModel.Party;

namespace Improbable.OnlineServices.Common.Analytics
{
    public class AnalyticsCommandLineArgs
    {
        public const string EndpointName = "analytics.endpoint";
        public const string AllowInsecureEndpointName = "analytics.allow-insecure-endpoint";
        public const string ConfigPathName = "analytics.config-file-path";

        [Option(EndpointName, HelpText =
            "Endpoint for analytics to be sent to. If not provided, then analytics are disabled")]
        public string Endpoint { get; set; }

        [Option(AllowInsecureEndpointName, Default = false, HelpText = "If set, allows http URLs for the endpoint")]
        public bool AllowInsecureEndpoints { get; set; }

        [Option(ConfigPathName, HelpText =
            "Set the path for the analytics configuration file. If not set, default values " +
            "are assumed for all analytics events.")]
        public string ConfigPath { get; set; }
    }
}
