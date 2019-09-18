using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Improbable.OnlineServices.Base.Server;
using Improbable.OnlineServices.Common.Analytics;
using Improbable.OnlineServices.Common.Analytics.ExceptionHandlers;
using Improbable.SpatialOS.Deployment.V1Alpha1;
using Improbable.SpatialOS.Platform.Common;
using Improbable.SpatialOS.Snapshot.V1Alpha1;
using Mono.Unix;
using Mono.Unix.Native;
using Prometheus;
using Serilog;
using Serilog.Formatting.Compact;

namespace DeploymentPool
{
    public class DeploymentPoolArgs : CommandLineArgs, IAnalyticsCommandLineArgs
    {
        [Option("minimum-ready-deployments", HelpText = "Minimum number of deployments to keep in the Ready state.", Default = 3)]
        public int MinimumReadyDeployments { get; set; }

        [Option("match-type", HelpText = "The match type this pool will maintain deployments for.", Default = "default_game")]
        public string MatchType { get; set; }

        [Option("deployment-name-prefix", HelpText = "The name for which all deployments started by the pool will start with.", Default = "")]
        public string DeploymentNamePrefix { get; set; }

        [Option("snapshot", HelpText = "The snapshot file to start deployments with.", Required = true)]
        public string SnapshotFilePath { get; set; }

        [Option("launch-config", HelpText = "The launch configuration to use for deployments started by the pool.", Required = true)]
        public string LaunchConfigFilePath { get; set; }

        [Option("assembly-name", HelpText = "The previously uploaded assembly to start deployments with.", Required = true)]
        public string AssemblyName { get; set; }

        [Option("project", HelpText = "The SpatialOS Project to run pooled deployments in.", Required = true)]
        public string SpatialProject { get; set; }

        [Option("cleanup", HelpText = "Clean up and stop any running deployments when shutting down the pool", Default = false)]
        public bool Cleanup { get; set; }

        // Performs basic validation on arguments. Must be called after the arguments have been parsed.
        // throws AggregateException (containing ArgumentExceptions) in the case of validation failures.
        public void Validate()
        {
            var errors = new List<ArgumentException>();
            if (MinimumReadyDeployments <= 0)
            {
                errors.Add(new ArgumentException($"MinimumReadyDeployments should be greater than 0. \"{MinimumReadyDeployments}\" was provided"));
            }
            if (!File.Exists(LaunchConfigFilePath))
            {
                errors.Add(new ArgumentException($"launch config file should exist. \"{LaunchConfigFilePath}\" was provided"));
            }
            if (!File.Exists(SnapshotFilePath))
            {
                errors.Add(new ArgumentException($"snapshot file should exist. \"{SnapshotFilePath}\" was provided"));
            }

            if (errors.Count > 0)
            {
                throw new AggregateException(errors);
            }
        }

        public string Endpoint { get; set; }
        public bool AllowInsecureEndpoints { get; set; }
        public string ConfigPath { get; set; }
        public string GcpKeyPath { get; set; }
        public string Environment { get; set; }
    }

    class Program
    {
        private static readonly string SpatialRefreshTokenEnvironmentVariable = "SPATIAL_REFRESH_TOKEN";
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(new RenderedCompactJsonFormatter())
                .Enrich.FromLogContext()
                .CreateLogger();

            // See https://support.microsoft.com/en-gb/help/821268/contention-poor-performance-and-deadlocks-when-you-make-calls-to-web-s
            ThreadPool.SetMaxThreads(100, 100);
            ThreadPool.SetMinThreads(50, 50);

            Parser.Default.ParseArguments<DeploymentPoolArgs>(args)
                .WithParsed(parsedArgs =>
                    {
                        parsedArgs.Validate();
                        var spatialRefreshToken = Environment.GetEnvironmentVariable(SpatialRefreshTokenEnvironmentVariable) ??
                                                  throw new Exception(
                                                      $"{SpatialRefreshTokenEnvironmentVariable} environment variable is required.");
                        if (spatialRefreshToken == "")
                        {
                            throw new ArgumentException("Refresh token should not be empty");
                        }
                        
                        IAnalyticsSender analyticsSender = new AnalyticsSenderBuilder("deployment_pool")
                            .WithCommandLineArgs(parsedArgs)
                            .With(new LogExceptionStrategy(Log.Logger))
                            .Build();
                        
                        var spatialDeploymentClient =
                            DeploymentServiceClient.Create(credentials: new PlatformRefreshTokenCredential(spatialRefreshToken));
                        var spatialSnapshotClient =
                            SnapshotServiceClient.Create(credentials: new PlatformRefreshTokenCredential(spatialRefreshToken));
                        var platformInvoker = new PlatformInvoker(parsedArgs, spatialDeploymentClient, spatialSnapshotClient, analyticsSender);


                        var cancelTokenSource = new CancellationTokenSource();
                        var cancelToken = cancelTokenSource.Token;

                        var metricsServer = new MetricServer(parsedArgs.MetricsPort).Start();
                        var dplPool = new DeploymentPool(parsedArgs, spatialDeploymentClient, platformInvoker, cancelToken, analyticsSender);
                        var dplPoolTask = Task.Run(() => dplPool.Start());
                        var unixSignalTask = Task.Run(() => UnixSignal.WaitAny(new[] { new UnixSignal(Signum.SIGINT), new UnixSignal(Signum.SIGTERM) }));
                        Task.WaitAny(dplPoolTask, unixSignalTask);

                        if (unixSignalTask.IsCompleted)
                        {
                            Log.Information($"Received UNIX signal {unixSignalTask.Result}");
                            Log.Information("Server shutting down...");
                            cancelTokenSource.Cancel();
                            metricsServer.StopAsync();
                            dplPoolTask.Wait();
                            Log.Information("Server stopped cleanly");
                        }
                        else
                        {
                            /* The server task has completed; we can just exit. */
                            Log.Information($"The deployment pool has stopped itself or encountered an unhandled exception {dplPoolTask.Exception}");
                        }
                    });
        }
    }
}
