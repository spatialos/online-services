using System;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Improbable.OnlineServices.Base.Server;
using Improbable.OnlineServices.Proto.Gateway;
using MemoryStore.Redis;
using Mono.Unix;
using Mono.Unix.Native;
using Serilog;
using Serilog.Formatting.Compact;

namespace GatewayInternal
{
    class GatewayInternalArgs : CommandLineArgs
    {
        [Option("redis_connection_string", HelpText = "Redis connection string.", Default = "localhost:6379")]
        public string RedisConnectionString { get; set; }
    }

    class Program
    {
        private const string RedisEnvironmentVariable = "REDIS_CONNECTION_STRING";

        static void Main(string[] args)
        {
            // Required to have enough I/O threads to handle Redis+gRPC traffic
            // See https://support.microsoft.com/en-gb/help/821268/contention-poor-performance-and-deadlocks-when-you-make-calls-to-web-s
            ThreadPool.SetMaxThreads(100, 100);
            ThreadPool.SetMinThreads(50, 50);

            Parser.Default.ParseArguments<GatewayInternalArgs>(args)
                .WithParsed(parsedArgs =>
                {
                    Log.Logger = new LoggerConfiguration()
                        .WriteTo.Console(new RenderedCompactJsonFormatter())
                        .Enrich.FromLogContext()
                        .CreateLogger();

                    //TODO(dom): remove in favour of just passing in via the cmd
                    var memoryStoreClientManager =
                        new RedisClientManager(Environment.GetEnvironmentVariable(RedisEnvironmentVariable) ??
                                               parsedArgs.RedisConnectionString);

                    var server = GrpcBaseServer.Build(parsedArgs);
                    server.AddService(
                        GatewayInternalService.BindService(new GatewayInternalServiceImpl(memoryStoreClientManager)));

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
                        Log.Information("The GatewayInternal server has stopped itself or encountered an unhandled exception.");
                    }
                });
        }
    }
}