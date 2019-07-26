using CommandLine;

namespace Improbable.OnlineServices.Common.Analytics
{
    public class AnalyticsCommandLineArgs
    {
        public const string EndpointName = "analytics.endpoint";
        public const string AllowInsecureEndpointName = "analytics.allowinsecureendpoint";

        [Option(EndpointName, HelpText =
            "Endpoint for analytics to be sent to. If not provided, then analytics are disabled")]
        public string Endpoint { get; set; }

        [Option(AllowInsecureEndpointName, Default = false, HelpText = "If set, allows http URLs for the endpoint")]
        public bool AllowInsecureEndpoints { get; set; }
    }
}