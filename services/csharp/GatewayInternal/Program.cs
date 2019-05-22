using System;
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
                    server.Start();
                    UnixSignal.WaitAny(new[] { new UnixSignal(Signum.SIGINT), new UnixSignal(Signum.SIGTERM) });
                    server.Shutdown();
                    Environment.Exit(0);
                });
        }
    }
}