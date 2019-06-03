using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Google.Rpc;
using Improbable.OnlineServices.Base.Server;
using Improbable.SpatialOS.Deployment.V1Alpha1;
using Improbable.SpatialOS.Platform.Common;
using Improbable.SpatialOS.Snapshot.V1Alpha1;
using Mono.Unix;
using Mono.Unix.Native;
using Prometheus;
using Prometheus.Advanced;
using Serilog;
using Serilog.Formatting.Compact;

namespace DeploymentPool
{
    public class DeploymentPoolArgs : CommandLineArgs
    {
        [Option("minimum-ready-deployments", HelpText = "Minimum number of deployments to keep in the Ready state.", Default = 3)]
        public int MinimumReadyDeployments { get; set; }

        [Option("match-type", HelpText = "The match type this pool will maintain deployments for.", Default = "default_game")]
        public string MatchType { get; set; }

        [Option("deployment-name-prefix", HelpText = "The name for which all deployments started by the pool will start with.", Default = "pooled_dpl_")]
        public string DeploymentNamePrefix { get; set; }

        [Option("snapshot", HelpText = "The snapshot file to start deployments with.", Required = true)]
        public string SnapshotFilePath { get; set; }

        [Option("launch-config", HelpText = "The launch configuration to use for deployments started by the pool.", Required = true)]
        public string LaunchConfigFilePath { get; set; }

        [Option("assembly-name", HelpText = "The previously uploaded assembly to start deployments with.", Required = true)]
        public string AssemblyName { get; set; }

        [Option("project", HelpText = "The SpatialOS Project to run pooled deployments in.", Required = true)]
        public string SpatialProject { get; set; }

        // Performs basic validation on arguments. Must be called after the arguments have been parsed.
        // throws AggregateException (containing ArgumentExceptions) in the case of validation failures.
        public void validate()
        {
            var errors = new List<ArgumentException>();
            if (MinimumReadyDeployments <= 0)
            {
                errors.Add(new ArgumentException($"MinimumReadyDeployments should be greater than 0. \"{MinimumReadyDeployments}\" was provided"));
            }
            if (!File.Exists(LaunchConfigFilePath))
            {
                errors.Add(new ArgumentException($"launch_config file should exist. \"{LaunchConfigFilePath}\" was provided"));
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

            ThreadPool.SetMinThreads(50, 50);

            Parser.Default.ParseArguments<DeploymentPoolArgs>(args)
                .WithParsed(parsedArgs =>
                    {
                        parsedArgs.validate();
                        var spatialRefreshToken = Environment.GetEnvironmentVariable(SpatialRefreshTokenEnvironmentVariable) ??
                                                  throw new Exception(
                                                      $"{SpatialRefreshTokenEnvironmentVariable} environment variable is required.");
                        var spatialDeploymentClient =
                            DeploymentServiceClient.Create(credentials: new PlatformRefreshTokenCredential(spatialRefreshToken));
                        var spatialSnapshotClient =
                            SnapshotServiceClient.Create(credentials: new PlatformRefreshTokenCredential(spatialRefreshToken));
                        var platformInvoker = new PlatformInvoker(parsedArgs, spatialDeploymentClient, spatialSnapshotClient);

                        var cancelTokenSource = new CancellationTokenSource();
                        var cancelToken = cancelTokenSource.Token;

                        var dplPool = new DeploymentPool(parsedArgs, spatialDeploymentClient, platformInvoker, cancelToken);
                        var dplPoolTask = Task.Run(() => dplPool.Start());

                        var unixSignalTask = new Task<int>(() =>
                            UnixSignal.WaitAny(new[] { new UnixSignal(Signum.SIGINT), new UnixSignal(Signum.SIGTERM) }));
                        unixSignalTask.Start();

                        Task.WaitAny(dplPoolTask, unixSignalTask);
                        if (unixSignalTask.IsCompleted)
                        {
                            Log.Information($"Received UNIX signal {unixSignalTask.Result}");
                            Log.Information("Server shutting down...");
                            cancelTokenSource.Cancel();
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