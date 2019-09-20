using System;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Mono.Unix;
using Mono.Unix.Native;
using Google.LongRunning;
using Improbable.OnlineServices.Base.Server;
using Improbable.OnlineServices.Common;
using Improbable.OnlineServices.Common.Analytics;
using Improbable.OnlineServices.Common.Analytics.ExceptionHandlers;
using Improbable.OnlineServices.Common.Interceptors;
using Improbable.OnlineServices.Proto.Gateway;
using Improbable.SpatialOS.Platform.Common;
using Improbable.SpatialOS.PlayerAuth.V2Alpha1;
using MemoryStore.Redis;
using Serilog;
using Serilog.Formatting.Compact;

namespace Gateway
{
    class GatewayArgs : CommandLineArgs, IAnalyticsCommandLineArgs
    {
        [Option("redis_connection_string", HelpText = "Redis connection string.", Default = "localhost:6379")]
        public string RedisConnectionString { get; set; }

        public string Endpoint { get; set; }
        public bool AllowInsecureEndpoints { get; set; }
        public string ConfigPath { get; set; }
        public string GcpKeyPath { get; set; }
        public string Environment { get; set; }
    }

    class Program
    {
        private const string SpatialRefreshTokenEnvironmentVariable = "SPATIAL_REFRESH_TOKEN";

        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(new RenderedCompactJsonFormatter())
                .Enrich.FromLogContext()
                .CreateLogger();

            // See https://support.microsoft.com/en-gb/help/821268/contention-poor-performance-and-deadlocks-when-you-make-calls-to-web-s
            // Experimentation shows we need the ThreadPool to always spin up threads for good performance under load
            ThreadPool.GetMaxThreads(out var workerThreads, out var ioThreads);
            ThreadPool.SetMinThreads(workerThreads, ioThreads);

            Parser.Default.ParseArguments<GatewayArgs>(args)
                .WithParsed(parsedArgs =>
                {
                    var spatialRefreshToken = Secrets.GetEnvSecret(SpatialRefreshTokenEnvironmentVariable);

                    var memoryStoreClientManager =
                        new RedisClientManager(parsedArgs.RedisConnectionString);

                    var playerAuthClient =
                        PlayerAuthServiceClient.Create(
                            credentials: new PlatformRefreshTokenCredential(spatialRefreshToken));

                    IAnalyticsSender analyticsSender = new AnalyticsSenderBuilder("gateway_gateway")
                        .WithCommandLineArgs(parsedArgs)
                        .With(new LogExceptionStrategy(Log.Logger))
                        .Build();

                    var server = GrpcBaseServer.Build(parsedArgs);
                    server.AddInterceptor(new PlayerIdentityTokenValidatingInterceptor(
                        playerAuthClient,
                        memoryStoreClientManager.GetRawClient(Database.CACHE)));
                    server.AddService(
                        GatewayService.BindService(new GatewayServiceImpl(memoryStoreClientManager, analyticsSender)));
                    server.AddService(
                        Operations.BindService(new OperationsServiceImpl(memoryStoreClientManager, playerAuthClient, analyticsSender)));

                    var serverTask = Task.Run(() => server.Start());
                    var signalTask = Task.Run(() => UnixSignal.WaitAny(new[] { new UnixSignal(Signum.SIGINT), new UnixSignal(Signum.SIGTERM) }));
                    Task.WaitAny(serverTask, signalTask);

                    if (signalTask.IsCompleted)
                    {
                        Log.Information($"Received UNIX signal {signalTask.Result}");
                        Log.Information("Server shutting down...");
                        server.Shutdown();
                        serverTask.Wait();
                        Log.Information("Server stopped cleanly");
                    }
                    else
                    {
                        /* The server task has completed; we can just exit. */
                        Log.Information("The Gateway server has stopped itself or encountered an unhandled exception.");
                    }
                });
        }
    }
}
