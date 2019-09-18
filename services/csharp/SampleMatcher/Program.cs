using System;
using System.Threading.Tasks;
using CommandLine;
using Improbable.OnlineServices.Base.Server;
using Improbable.OnlineServices.Common.Analytics;
using Improbable.OnlineServices.Common.Analytics.ExceptionHandlers;
using Mono.Unix;
using Mono.Unix.Native;
using Serilog;
using Serilog.Formatting.Compact;

namespace Improbable.OnlineServices.SampleMatcher
{
    class MatcherArgs : CommandLineArgs, IAnalyticsCommandLineArgs
    {
        public string Endpoint { get; set; }
        public bool AllowInsecureEndpoints { get; set; }
        public string ConfigPath { get; set; }
        public string GcpKeyPath { get; set; }
        public string Environment { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console(new RenderedCompactJsonFormatter())
                .Enrich.FromLogContext()
                .CreateLogger();

            Parser.Default.ParseArguments<MatcherArgs>(args)
                .WithParsed(parsedArgs =>
                {
                    IAnalyticsSender analyticsSender = new AnalyticsSenderBuilder("gateway_matcher")
                        .WithCommandLineArgs(parsedArgs)
                        .With(new LogExceptionStrategy(Log.Logger))
                        .Build();

                    var matcher = new Matcher(analyticsSender);
                    var matcherTask = new Task(() => { matcher.Start(); });
                    var unixSignalTask = new Task<int>(() =>
                    {
                        return UnixSignal.WaitAny(new[]
                            {new UnixSignal(Signum.SIGINT), new UnixSignal(Signum.SIGTERM)});
                    });

                    matcherTask.Start();
                    Console.WriteLine("Matcher started up");
                    unixSignalTask.Start();

                    Task.WaitAny(matcherTask, unixSignalTask);
                    if (unixSignalTask.IsCompleted)
                    {
                        Console.WriteLine($"Received UNIX signal {unixSignalTask.Result}");
                        Console.WriteLine("Matcher shutting down...");
                        matcher.Stop();
                        matcherTask.Wait(TimeSpan.FromSeconds(10));
                        Console.WriteLine("Matcher stopped cleanly");
                    }
                    else
                    {
                        /* The matcher task has completed; we can just exit. */
                        Console.WriteLine("The matcher has stopped itself or encountered an unhandled exception.");
                    }

                    Environment.Exit(0);
                });
        }
    }
}
