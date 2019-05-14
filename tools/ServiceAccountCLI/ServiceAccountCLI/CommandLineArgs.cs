
using System;
using CommandLine;

namespace ServiceAccountCLI
{
    [Verb("create", HelpText = "Create a new service account")]
    public class CreateOptions
    {
        [Option("project_name", HelpText = "The name of your spatial project", Required = true)]
        public string ProjectName { get; set; }
        
        [Option("service_account_name", HelpText = "The name of the service account", Required = true)]
        public string ServiceAccountName { get; set; }
        
        [Option("refresh_token_output_file", HelpText = "The name of a file to output the refresh token to", Required = true)]
        public string RefreshTokenFile { get; set; }

        [Option("lifetime_minutes", HelpText = "The lifetime of the service account in minutes", Default = 0)]
        public int LifetimeMinutes { get; set; }
        
        [Option("lifetime_hours", HelpText = "The lifetime of the service account in hours", Default = 0)]
        public int LifetimeHours { get; set; }
        
        [Option("lifetime_days", HelpText = "The lifetime of the service account in days", Default = 0)]
        public int LifetimeDays { get; set; }
        
        [Option("project_write", HelpText = "Whether or not project write access is needed", Default = false)]
        public bool ProjectWrite { get; set; }
        
        [Option("metrics_read", HelpText = "Whether or not read access to metrics is needed", Default = false)]
        public bool MetricsRead { get; set; }
    }
    
    [Verb("list", HelpText = "List existing service accounts")]
    public class ListOptions
    {
        [Option("project_name", HelpText = "The name of your spatial project", Required = true)]
        public string ProjectName { get; set; }
    }
    
    [Verb("delete", HelpText = "Delete an existing service account")]
    public class DeleteOptions
    {
        [Option("service_account_id", HelpText = "The ID of the service account", Required = true)]
        public Int64 ServiceAccountId { get; set; }
    }
}