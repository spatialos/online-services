using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Improbable.SpatialOS.Deployment.V1Alpha1;
using Improbable.SpatialOS.Snapshot.V1Alpha1;
using Serilog;
using Serilog.Core;

namespace DeploymentPool
{
    public class DeploymentPoolManager
    {
        private static readonly Random _random = new Random();
        private bool _shutdown;
        private readonly string _matchType;
        private readonly int _minimumReadyDeployments;
        private readonly string _deploymentNamePrefix;
        private readonly string _snapshotFilePath;
        private readonly string _launchConfigFilePath;
        private readonly string _assemblyName;

        private readonly DeploymentServiceClient _deploymentServiceClient;
        private readonly SnapshotServiceClient _snapshotServiceClient;

        public DeploymentPoolManager(
            DeploymentPoolArgs args,
            DeploymentServiceClient deploymentServiceClient,
            SnapshotServiceClient snapshotServiceClient)
        {

            _matchType = args.MatchType;
            _minimumReadyDeployments = args.MinimumReadyDeployments;
            _spatialProject = args.SpatialProject;
            _deploymentNamePrefix = args.DeploymentNamePrefix;
            _snapshotFilePath = args.SnapshotFilePath;
            _launchConfigFilePath = args.LaunchConfigFilePath;
            _assemblyName = args.AssemblyName;
            _deploymentServiceClient = deploymentServiceClient;
            _snapshotServiceClient = snapshotServiceClient;
        }

        private readonly string _spatialProject;

        public Task Start()
        {
            return Task.Run(() => run());
        }

        public void StopAll()
        {
            // Basic views do not include player information
            var matchDeployments = _deploymentServiceClient
                .ListDeployments(new ListDeploymentsRequest
                {
                    ProjectName = _spatialProject,
                    PageSize = 50,
                    DeploymentStoppedStatusFilter = ListDeploymentsRequest.Types.DeploymentStoppedStatusFilter
                        .NotStoppedDeployments,
                    View = ViewType.Basic
                })
                .Where(d => d.Tag.Contains(_matchType));

            var stopTasks = new Task[matchDeployments.Count()];
            for (int i = 0; i < matchDeployments.Count(); i++)
            {
                var matchDeployment = matchDeployments.ElementAt(i);
                stopTasks[i] = Task.Run(() =>
                {
                    Log.Logger.Information("Stopping {}", matchDeployment.Name);
                    return stopDeployment(matchDeployment).GetAwaiter().GetResult();
                });
            }

            Log.Logger.Information("WARNING: Stopping all running {} deployments", _matchType);
            Task.WaitAll(stopTasks);
            Log.Logger.Information("All deployments have been stopped.");
        }

        public void Shutdown()
        {
            _shutdown = true;
        }

        public void run()
        {
            while (!_shutdown)
            {
                try
                {
                    var matchDeployments = _deploymentServiceClient
                        .ListDeployments(new ListDeploymentsRequest
                        {
                            ProjectName = _spatialProject,
                            PageSize = 50,
                            DeploymentStoppedStatusFilter = ListDeploymentsRequest.Types.DeploymentStoppedStatusFilter
                                .NotStoppedDeployments,
                            View = ViewType.Basic
                        })
                        .Where(d => d.Tag.Contains(_matchType));

                    checkForRequiredDeployments(matchDeployments);
                    checkForFreshlyStartedDeployments(matchDeployments);
                    checkForCompletedDeployments(matchDeployments);
                }
                catch (RpcException e)
                {
                    Log.Logger.Information($"Caught unexpected RpcException {e.Message}. Aborting this iteration...");
                }

                Thread.Sleep(TimeSpan.FromSeconds(10));
            }

            // TODO: Remove this once everything works
            StopAll();
            Log.Logger.Information("Shutdown signal received. Pool has stopped.");
        }

        // Checks for discrepencies between Running+Starting deployments and the requested minimum number.
        private void checkForRequiredDeployments(IEnumerable<Deployment> existingDeployments)
        {
            var readyDeployments = existingDeployments.Where(d => d.Tag.Contains("ready"));
            var startingDeployments = existingDeployments.Where(d => d.Tag.Contains("starting"));
            Log.Logger.Information(
                $"{readyDeployments.Count()}/{_minimumReadyDeployments} deployments ready for use; {startingDeployments.Count()} starting up.");

            if (readyDeployments.Count() + startingDeployments.Count() < _minimumReadyDeployments)
            {
                Log.Logger.Information(
                    $"Missing {_minimumReadyDeployments - readyDeployments.Count() - startingDeployments.Count()}. Starting...");

                var diff = _minimumReadyDeployments - readyDeployments.Count() - startingDeployments.Count();
                var startTasks = new Task[diff];
                for (int i = 0; i < diff; i++)
                {
                    var startTask = Task.Run(() => startDeployment(_random.Next()));
                    startTasks[i] = startTask;
                }
                Task.WaitAll(startTasks);
            }
        }

        // Checks that previously started deployments have finished starting up, transfer them into the Ready state
        private void checkForFreshlyStartedDeployments(IEnumerable<Deployment> existingDeployments)
        {
            var startingDeployments = existingDeployments.Where(d => d.Tag.Contains("starting"));
            foreach (var startingDeployment in startingDeployments)
            {
                if (startingDeployment.Status == Deployment.Types.Status.Running || startingDeployment.Status == Deployment.Types.Status.Error)
                {
                    if (startingDeployment.Status == Deployment.Types.Status.Error)
                    {
                        startingDeployment.Tag.Add("complete");
                    }
                    if (startingDeployment.Status == Deployment.Types.Status.Running)
                    {
                        startingDeployment.Tag.Add("ready");
                    }
                    startingDeployment.Tag.Remove("starting");

                    _deploymentServiceClient.UpdateDeploymentAsync(new UpdateDeploymentRequest
                    {
                        Deployment = startingDeployment,
                    });
                }
            }
        }

        // Checks if any deployments have finished, and shuts them down.
        private void checkForCompletedDeployments(IEnumerable<Deployment> existingDeployments)
        {
            var completeDeployments = existingDeployments.Where(d => d.Tag.Contains("complete") && !d.Tag.Contains("stopping"));
            var stoppingDeployments = existingDeployments.Where(d => d.Tag.Contains("stopping"));
            if (completeDeployments.Any() || stoppingDeployments.Any())
            {
                Log.Logger.Information($"{completeDeployments.Count()} deployments to shut down. {stoppingDeployments} in progress.");
            }
            foreach (var completeDeployment in completeDeployments)
            {
                Task.Run(() => stopDeployment(completeDeployment));
            }
        }

        private async Task<bool> stopDeployment(Deployment deployment)
        {
            // Set the stopping tag
            deployment.Tag.Add("stopping");
            await _deploymentServiceClient.UpdateDeploymentAsync(new UpdateDeploymentRequest
            {
                Deployment = deployment,
            });

            var stopRequest = new StopDeploymentRequest
            {
                ProjectName = deployment.ProjectName,
                Id = deployment.Id
            };
            await _deploymentServiceClient.StopDeploymentAsync(stopRequest);
            return true;
        }

        private void startDeployment(int i)
        {
            var newDeploymentName = _deploymentNamePrefix + i;
            Log.Logger.Information("Starting new deployment named {}", newDeploymentName);
            var snapshotId = createSnapshotId(newDeploymentName);
            var launchConfig = getLaunchConfig();
            var createDeploymentRequest = new CreateDeploymentRequest
            {
                Deployment = new Deployment
                {
                    Name = newDeploymentName,
                    ProjectName = _spatialProject,
                    Description = "Launched by Deployment Pool",
                    AssemblyId = _assemblyName,
                    LaunchConfig = launchConfig,
                    StartingSnapshotId = snapshotId,
                }
            };
            createDeploymentRequest.Deployment.Tag.Add("starting");
            createDeploymentRequest.Deployment.Tag.Add(_matchType);
            createDeploymentRequest.Deployment.WorkerConnectionCapacities.Add(
                new WorkerCapacity
                {
                    WorkerType = "External",
                    MaxCapacity = 10,
                }
            );

            // Once operation is created, deployment should exist.
            var createOp = _deploymentServiceClient.CreateDeployment(createDeploymentRequest);
            Task.Run(() =>
            {
                var completed = createOp.PollUntilCompleted();
                if (completed.IsFaulted)
                {
                    Log.Logger.Information("Failed to start deployment {DplName}", createDeploymentRequest.Deployment.Name);
                }
                else if (completed.IsCompleted)
                {
                    Log.Logger.Information("Deployment {DplName} started succesfully", completed.Result.Name);
                }
                else
                {
                    Log.Logger.Information("Something went wrong starting deployment {DplName}", completed.Result.Name);
                }
            });
        }

        private string createSnapshotId(string deploymentName)
        {
            var snapshot = File.ReadAllBytes(_snapshotFilePath);
            var checksum = Convert.ToBase64String(MD5.Create().ComputeHash(snapshot));

            var response = _snapshotServiceClient.UploadSnapshot(new UploadSnapshotRequest
            {
                Snapshot = new Snapshot
                {
                    ProjectName = _spatialProject,
                    DeploymentName = deploymentName,
                    Checksum = checksum,
                    Size = snapshot.Length
                }
            });

            var httpRequest = WebRequest.Create(response.UploadUrl) as HttpWebRequest;
            httpRequest.Method = "PUT";
            httpRequest.ContentLength = response.Snapshot.Size;
            httpRequest.Headers.Set("Content-MD5", response.Snapshot.Checksum);
            using (var dataStream = httpRequest.GetRequestStream())
            {
                var bytesToSend = snapshot;
                dataStream.Write(bytesToSend, 0, bytesToSend.Length);
            }

            httpRequest.GetResponse();


            _snapshotServiceClient.ConfirmUpload(new ConfirmUploadRequest
            {
                DeploymentName = response.Snapshot.DeploymentName,
                Id = response.Snapshot.Id,
                ProjectName = response.Snapshot.ProjectName
            });

            Log.Logger.Information("Uploaded new snapshot at Id {}", response.Snapshot.Id);
            return response.Snapshot.Id;
        }

        private LaunchConfig getLaunchConfig()
        {
            var jsonString = ReadFile(_launchConfigFilePath);
            var launchConfig = new LaunchConfig
            {
                ConfigJson = jsonString
            };
            return launchConfig;
        }

        private String ReadFile(string filename)
        {
            return File.ReadAllText(filename);
        }
    }
}