using System;
using System.Threading.Tasks;
using CommandLine;
using Mono.Unix;
using Mono.Unix.Native;

namespace Improbable.OnlineServices.SampleMatcher
{
    public class SampleMatcherArguments
    {
        [Option("ignore_pool", HelpText = "Doesn't read from any metadata service to ascertain state of deployments")]
        public bool IgnorePool { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<SampleMatcherArguments>(args)
                .WithParsed(parsedArgs =>
                {
                    var matcher = (parsedArgs.IgnorePool)
                        ? (Improbable.OnlineServices.Base.Matcher.Matcher) new StandaloneMatcher()
                        : (Improbable.OnlineServices.Base.Matcher.Matcher) new PoolMatcher();
                    var matcherTask = new Task(() => { matcher.Start(); });
                    var unixSignalTask = new Task<int>(() =>
                    {
                        return UnixSignal.WaitAny(new[] { new UnixSignal(Signum.SIGINT), new UnixSignal(Signum.SIGTERM) });
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
