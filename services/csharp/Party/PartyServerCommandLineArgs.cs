using System;
using CommandLine;
using Improbable.OnlineServices.Base.Server;

namespace Party
{
    public class PartyServerCommandLineArgs : CommandLineArgs
    {
        [Option("redis_connection_string", HelpText = "The connection string for redis (host:port)",
            Default = "127.0.0.1:6379")]
        public string RedisConnectionString { get; set; }

        [Option("default_min_members", HelpText = "The default amount for the minimum amount of players for a party",
            Default = 1)]
        public int DefaultMinMembers { get; set; }

        [Option("default_max_members", HelpText = "The default amount for the maximum amount of players for a party",
            Default = int.MaxValue)]
        public int DefaultMaxMembers { get; set; }

        public void Validate()
        {
            if (DefaultMinMembers < 0)
            {
                throw new ArgumentException("DefaultMinMembers cannot be negative");
            }

            if (DefaultMaxMembers < 0)
            {
                throw new ArgumentException("DefaultMinMembers cannot be negative");
            }
        }
    }
}