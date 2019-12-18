namespace Improbable.OnlineServices.Common.Analytics
{
    public class AnalyticsCommandLineArgs : IAnalyticsCommandLineArgs
    {
        public const string EndpointName = "analytics.endpoint";
        public const string AllowInsecureEndpointName = "analytics.allow-insecure-endpoint";
        public const string ConfigPathName = "analytics.config-file-path";
        public const string GcpKeyPathName = "analytics.gcp-key-path";
        public const string EnvironmentName = "event.environment";
        public const string SchemaName = "event.schema";

        public string Endpoint { get; set; }
        public bool AllowInsecureEndpoints { get; set; }
        public string ConfigPath { get; set; }
        public string GcpKeyPath { get; set; }
        public string Environment { get; set; }
        public string EventSchema { get; set; }
    }
}
