using System;
using System.IO;
using CommandLine;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Improbable.SpatialOS.Platform.Common;
using Improbable.SpatialOS.ServiceAccount.V1Alpha1;

namespace ServiceAccountCLI
{
    class Program
    {
        private static readonly ServiceAccountServiceClient ServiceAccountServiceClient =
            ServiceAccountServiceClient.Create(credentials: PlatformRefreshTokenCredential.AutoDetected); 
        
        static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<CreateOptions, ListOptions, DeleteOptions>(args).MapResult(
                (CreateOptions opts) => CreateServiceAccount(opts),
                (ListOptions opts) => ListServiceAccounts(opts),
                (DeleteOptions opts) => DeleteServiceAccount(opts),
                 errs => 1
                );
        }

        private static int CreateServiceAccount(CreateOptions opts)
        {
            var parsedLifetime = TimeSpan.Zero;
            try
            {
                parsedLifetime = TimeSpan.Parse(opts.Lifetime);
            }
            catch (FormatException)
            {
                Console.WriteLine($"Failed to parse lifetime: {opts.Lifetime}");
                Console.WriteLine("Expected it to be in a format that can be parsed as a TimeSpan.");
                Console.WriteLine("E.g. 1.2:15 is 1 day, 2 hours and 15 minutes.");
                return 1;
            }
            
            if (File.Exists(opts.RefreshTokenFile))
            {
                Console.WriteLine($"Refresh token output file {opts.RefreshTokenFile} already exists.");
                Console.WriteLine("Aborting service account creation. Please delete / move it before running again.");
                return 1;
            }
                
            var projectPermissionVerbs = new RepeatedField<Permission.Types.Verb> {Permission.Types.Verb.Read};

            if (opts.ProjectWrite)
            {
                Console.WriteLine("Granting the service account project write access.");
                projectPermissionVerbs.Add(Permission.Types.Verb.Write);
            }

            var projectPermission = new Permission
            {
                Parts = {new RepeatedField<string> {"prj", opts.ProjectName, "*"}},
                Verbs = {projectPermissionVerbs}
            };

            var permissions = new RepeatedField<Permission> {projectPermission};

            if (opts.MetricsRead)
            {
                Console.WriteLine("Granting the service account metrics read access.");
                var metricsReadPermissions = new Permission
                {
                    Parts = {new RepeatedField<string> {"srv", "*"}},
                    Verbs = {new RepeatedField<Permission.Types.Verb> {Permission.Types.Verb.Read}}
                };
                permissions.Add(metricsReadPermissions);
            }

            var serviceAccount = ServiceAccountServiceClient.CreateServiceAccount(new CreateServiceAccountRequest
            {
                Name = opts.ServiceAccountName,
                ProjectName = opts.ProjectName,
                Permissions = {permissions},
                Lifetime = Duration.FromTimeSpan(parsedLifetime),
            });
            
            Console.WriteLine($"Service account created with ID {serviceAccount.Id}");
            Console.WriteLine($"Writing service account refresh token to {opts.RefreshTokenFile}.");
            
            using (var sr = File.CreateText(opts.RefreshTokenFile)) 
            {
                sr.WriteLine(serviceAccount.Token);
            }
            return 0;
        }

        private static int ListServiceAccounts(ListOptions opts)
        {
            var response = ServiceAccountServiceClient.ListServiceAccounts(new ListServiceAccountsRequest
            {
                ProjectName = opts.ProjectName,
            });
            foreach (var serviceAccount in response)
            {
                Console.WriteLine("-----------------------------");
                Console.WriteLine($"Name: {serviceAccount.Name}");
                Console.WriteLine($"ID: {serviceAccount.Id}");
                Console.WriteLine($"Creation time : {serviceAccount.CreationTime}");
                
                var daysUntilExpiry =
                    Math.Floor((serviceAccount.ExpirationTime.ToDateTime() - DateTime.UtcNow).TotalDays);
                Console.WriteLine(daysUntilExpiry < 0
                    ? $"Expired {Math.Abs(daysUntilExpiry)} day(s) ago"
                    : $"Expiring in {daysUntilExpiry} day(s)");
                
                Console.WriteLine($"Permissions: {serviceAccount.Permissions}");
                Console.WriteLine("-----------------------------");
            }
            return 0;
        }

        private static int DeleteServiceAccount(DeleteOptions opts)
        {
            ServiceAccountServiceClient.DeleteServiceAccount(new DeleteServiceAccountRequest
            {
                Id = opts.ServiceAccountId
            });
            
            Console.WriteLine($"Service account with ID {opts.ServiceAccountId} deleted.");
            return 0;
        }
    }
}