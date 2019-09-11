using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Grpc.Core;
using Improbable.OnlineServices.Base.Server;
using Improbable.OnlineServices.Base.Server.Interceptors;
using Improbable.OnlineServices.Common;
using Improbable.OnlineServices.Common.Interceptors;
using Improbable.OnlineServices.Proto.Metadata;
using MemoryStore;
using MemoryStore.Redis;
using Mono.Unix;
using Mono.Unix.Native;
using Serilog;
using Serilog.Formatting.Compact;

namespace DeploymentMetadata
{
    class DeploymentMetadataArgs : CommandLineArgs
    {
        [Option("redis_connection_string", HelpText = "Redis connection string.", Default = "localhost:6379")]
        public string RedisConnectionString { get; set; }
    }

    class Program
    {
        private const string ServerSecretEnvironmentVariable = "DEPLOYMENT_METADATA_SERVER_SECRET";

        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(new RenderedCompactJsonFormatter())
                .Enrich.FromLogContext()
                .CreateLogger();

            // See https://support.microsoft.com/en-gb/help/821268/contention-poor-performance-and-deadlocks-when-you-make-calls-to-web-s
            // Experimentation shows we need the ThreadPool to always spin up threads for good performance under load
            ThreadPool.GetMaxThreads(out var workerThreads, out var ioThreads);
            ThreadPool.SetMinThreads(workerThreads, ioThreads);

            Parser.Default.ParseArguments<DeploymentMetadataArgs>(args)
                .WithParsed(parsedArgs =>
                {
                    var serverSecret = Secrets.GetEnvSecret(ServerSecretEnvironmentVariable);

                    var memoryStoreClientManager =
                        new RedisClientManager(parsedArgs.RedisConnectionString, Database.Metadata);

                    var server = GrpcBaseServer.Build(parsedArgs);

                    server.AddInterceptor(new SecretCheckingInterceptor(serverSecret));

                    server.AddInterceptor(new ExceptionMappingInterceptor(
                        new Dictionary<Type, StatusCode>
                        {
                            {typeof(EntryNotFoundException), StatusCode.NotFound},
                            {typeof(EntryAlreadyExistsException), StatusCode.AlreadyExists},
                            {typeof(FailedConditionException), StatusCode.FailedPrecondition}
                        }));
                    server.AddService(
                        DeploymentMetadataService.BindService(new DeploymentMetadataImpl(memoryStoreClientManager)));

                    var serverTask = Task.Run(() => server.Start());
                    var signalTask = Task.Run(() =>
                        UnixSignal.WaitAny(new[] { new UnixSignal(Signum.SIGINT), new UnixSignal(Signum.SIGTERM) }));
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
                        Log.Information(
                            "The DeploymentMetadata server has stopped itself or encountered an unhandled exception.");
                    }
                });
        }
    }
}
