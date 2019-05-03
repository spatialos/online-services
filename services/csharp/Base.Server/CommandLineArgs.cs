using System;
using CommandLine;

namespace Improbable.OnlineServices.Base.Server
{
    public class CommandLineArgs
    {
        [Option("grpc_service_port", HelpText = "Port for gRPC Services.", Default = 4040)]
        public int GrpcServicePort { get; set; }

        [Option("metrics_port", HelpText = "Port to expose Prometheus Metrics.", Default = 8080)]
        public int MetricsPort { get; set; }

        // Due to the way the command line parsing framework is made, this can never be set to false and will always be 
        // true.
        [Obsolete("Deprecated, use disable_prometheus instead.")]
        [Option("enable_prometheus", HelpText = "Deprecated: use disable_prometheus instead", Default = true)]
        public bool EnablePrometheus { get; set; }

        [Option("disable_prometheus", HelpText = "If present, Prometheus will not be used for gathering metrics.",
            Default = false)]
        public bool DisablePrometheus { get; set; }
    }
}