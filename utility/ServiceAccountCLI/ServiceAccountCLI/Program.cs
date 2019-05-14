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
        private static readonly TimeSpan DefaultLifetime = new TimeSpan(1, 0, 0, 0);
        private const string DefaultLifetimeDescription = "1 day";
        
        private static readonly ServiceAccountServiceClient ServiceAccountServiceClient =
            ServiceAccountServiceClient.Create(credentials: PlatformRefreshTokenCredential.AutoDetected); 
        
        static int Main(string[] args)
        {
            return Parser.Default.ParseArguments<CreateOptions, ListOptions, DeleteOptions>(args).MapResult(
                (CreateOptions opts) => CreateServiceAccount(opts.ServiceAccountName, opts.ProjectName,
                    opts.RefreshTokenFile, opts.LifetimeMinutes, opts.LifetimeHours, opts.LifetimeDays,
                    opts.ProjectWrite, opts.MetricsRead),
                (ListOptions opts) => ListServiceAccounts(opts.ProjectName),
                (DeleteOptions opts) => DeleteServiceAccount(opts.ServiceAccountId),
                 errs => 1
                );
        }

        private static int CreateServiceAccount(string projectName, string serviceAccountName, string refreshTokenOutputFile,
            int lifeTimeMinutes, int lifetimeHours, int lifeTimeDays, bool projectWrite, bool metricsRead)
        {
            if (File.Exists(refreshTokenOutputFile))
            {
                Console.WriteLine($"Refresh token output file {refreshTokenOutputFile} already exists.");
                Console.WriteLine("Aborting service account creation. Please delete / move it before running again.");
                return 1;
            }
                
            var lifetime = DefaultLifetime;
            if (lifeTimeMinutes == 0 && lifetimeHours == 0 && lifeTimeDays == 0)
            {
                Console.WriteLine($"No lifetime value provided, using the default: {DefaultLifetimeDescription}");
            }
            else
            {
                lifetime = new TimeSpan(lifeTimeDays, lifetimeHours, lifeTimeMinutes, 0 /* No seconds. */); 
            }
            
            var projectPermissionVerbs = new RepeatedField<Permission.Types.Verb> {Permission.Types.Verb.Read};

            if (projectWrite)
            {
                Console.WriteLine("Granting the service account project write access.");
                projectPermissionVerbs.Add(Permission.Types.Verb.Write);
            }

            var projectPermission = new Permission
            {
                Parts = {new RepeatedField<string> {"prj", projectName, "*"}},
                Verbs = {projectPermissionVerbs}
            };

            var permissions = new RepeatedField<Permission> {projectPermission};

            if (metricsRead)
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
                Name = serviceAccountName,
                ProjectName = projectName,
                Permissions = {permissions},
                Lifetime = Duration.FromTimeSpan(lifetime),
            });
            
            Console.WriteLine($"Service account created with ID {serviceAccount.Id}");
            Console.WriteLine($"Writing service account refresh token to {refreshTokenOutputFile}.");
            
            using (var sr = File.CreateText(refreshTokenOutputFile)) 
            {
                sr.WriteLine(serviceAccount.Token);
            }
            return 0;
        }

        private static int ListServiceAccounts(string projectName)
        {
            var response = ServiceAccountServiceClient.ListServiceAccounts(new ListServiceAccountsRequest
            {
                ProjectName = projectName,
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

        private static int DeleteServiceAccount(Int64 serviceAccountId)
        {
            ServiceAccountServiceClient.DeleteServiceAccount(new DeleteServiceAccountRequest
            {
                Id = serviceAccountId
            });
            
            Console.WriteLine($"Service account with ID {serviceAccountId} deleted.");
            return 0;
        }
    }
}