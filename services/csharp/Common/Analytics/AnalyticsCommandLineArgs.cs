using CommandLine;

namespace Improbable.OnlineServices.Common.Analytics
{
    public class AnalyticsCommandLineArgs : IAnalyticsCommandLineArgs
    {
        public const string EndpointName = "analytics.endpoint";
        public const string AllowInsecureEndpointName = "analytics.allow-insecure-endpoint";
        public const string ConfigPathName = "analytics.config-file-path";
        public const string GcpKeyPathName = "analytics.gcp-key-path";
        public const string EnvironmentName = "analytics.environment";

        public string Endpoint { get; set; }
        public bool AllowInsecureEndpoints { get; set; }
        public string ConfigPath { get; set; }
        public string GcpKeyPath { get; set; }
        public string Environment { get; set; }
    }
}
