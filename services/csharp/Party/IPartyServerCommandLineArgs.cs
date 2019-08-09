using System;
using CommandLine;
using Improbable.OnlineServices.Base.Server;
using Improbable.OnlineServices.Common.Analytics;

namespace Party
{
    public interface IPartyServerCommandLineArgs : ICommandLineArgs
    {
        [Option("redis_connection_string", HelpText = "The connection string for redis (host:port)",
            Default = "127.0.0.1:6379")]
        string RedisConnectionString { get; set; }

        [Option("default_min_members", HelpText = "The default amount for the minimum amount of players for a party",
            Default = 1)]
        int DefaultMinMembers { get; set; }

        [Option("default_max_members", HelpText = "The default amount for the maximum amount of players for a party",
            Default = int.MaxValue)]
        int DefaultMaxMembers { get; set; }
    }
}
