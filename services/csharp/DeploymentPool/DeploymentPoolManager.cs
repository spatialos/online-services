using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Improbable.SpatialOS.Deployment.V1Alpha1;
using Improbable.SpatialOS.Snapshot.V1Alpha1;
using Serilog;

namespace DeploymentPool
{
    public class DeploymentPoolManager
    {
        private const string READY_TAG = "ready";
        private const string STARTING_TAG = "starting";
        private const string STOPPING_TAG = "stopping";
        private const string COMPLETED_TAG = "completed";

        private static readonly Random _random = new Random();
        private bool _shutdown;
        private readonly string matchType;
        private readonly int minimumReadyDeployments;
        private readonly string deploymentNamePrefix;
        private readonly string snapshotFilePath;
        private readonly string launchConfigFilePath;
        private readonly string assemblyName;

        private readonly SnapshotServiceClient _snapshotServiceClient;
        private readonly DeploymentServiceClient _deploymentServiceClient;

        public DeploymentPoolManager(
            DeploymentPoolArgs args,
            DeploymentServiceClient deploymentServiceClient,
            SnapshotServiceClient snapshotServiceClient)
        {

            matchType = args.MatchType;
            minimumReadyDeployments = args.MinimumReadyDeployments;
            _spatialProject = args.SpatialProject;
            deploymentNamePrefix = args.DeploymentNamePrefix;
            snapshotFilePath = args.SnapshotFilePath;
            launchConfigFilePath = args.LaunchConfigFilePath;
            assemblyName = args.AssemblyName;
            _deploymentServiceClient = deploymentServiceClient;
            _snapshotServiceClient = snapshotServiceClient;
        }

        private readonly string _spatialProject;

        public Task Start()
        {
            return Task.Run(async () => await Run());
        }

        public void StopAll()
        {
            // Basic views do not include player information
            var matchDeployments = ListDeployments();

            var stopTasks = new Task[matchDeployments.Count()];
            for (int i = 0; i < matchDeployments.Count(); i++)
            {
                var matchDeployment = matchDeployments.ElementAt(i);
                stopTasks[i] = Task.Run(() =>
                {
                    StopDeployment(matchDeployment);
                });
            }

            Log.Logger.Warning("Stopping all running {} deployments", matchType);
            Task.WaitAll(stopTasks);
            Log.Logger.Information("All deployments have been stopped.");
        }

        public void Shutdown()
        {
            _shutdown = true;
        }

        public async Task Run()
        {
            while (!_shutdown)
            {
                var matchDeployments = ListDeployments();
                var actions = GetRequiredActions(matchDeployments);
                ApplyActions(actions);
                await Task.Delay(TimeSpan.FromSeconds(10));
            }

            // TODO: Remove this once everything works
            StopAll();
            Log.Logger.Information("Shutdown signal received. Pool has stopped.");
        }

        public IEnumerable<DeploymentAction> GetRequiredActions(IEnumerable<Deployment> matchDeployments)
        {
            try
            {
                var actions = GetCreationActions(matchDeployments);
                actions = actions.Concat(GetUpdateActions(matchDeployments));
                actions = actions.Concat(GetStopActions(matchDeployments));
                return actions;
            }
            catch (RpcException e)
            {
                Log.Logger.Information($"Caught unexpected RpcException {e.Message}. Aborting this iteration...");
            }

            return new List<DeploymentAction>();
        }

        private void ApplyActions(IEnumerable<DeploymentAction> actionToTake)
        {
            var tasks = new Task[actionToTake.Count()];
            for (int i = 0; i < actionToTake.Count(); i++)
            {
                var deploymentAction = actionToTake.ElementAt(i);
                switch (deploymentAction.GetActionType())
                {
                    case DeploymentAction.ActionType.CREATE:
                        tasks[i] = Task.Run(() => StartDeployment(deploymentNamePrefix + _random.Next(10000)));
                        break;
                    case DeploymentAction.ActionType.UPDATE:
                        tasks[i] = Task.Run(() => UpdateDeployment(deploymentAction.GetDeployment()));
                        break;
                    case DeploymentAction.ActionType.STOP:
                        tasks[i] = Task.Run(() => StopDeployment(deploymentAction.GetDeployment()));
                        break;
                    default:
                        throw new Exception("Unknown type encountered!");
                }
            }

            Task.WaitAll(tasks);
        }

        // Checks for discrepencies between Running+Starting deployments and the requested minimum number.
        private IEnumerable<DeploymentAction> GetCreationActions(IEnumerable<Deployment> existingDeployments)
        {
            List<DeploymentAction> creationActions = new List<DeploymentAction>();
            var readyDeployments = existingDeployments.Count(d => d.Tag.Contains(READY_TAG));
            var startingDeployments = existingDeployments.Count(d => d.Tag.Contains(STARTING_TAG));
            var availableDeployments = readyDeployments + startingDeployments;
            Log.Logger.Information(
                $"{readyDeployments}/{minimumReadyDeployments} deployments ready for use; {startingDeployments} starting up.");

            if (availableDeployments < minimumReadyDeployments)
            {
                var diff = minimumReadyDeployments - availableDeployments;
                Log.Logger.Information($"Missing {diff} deployments");

                for (int i = 0; i < diff; i++)
                {
                    creationActions.Add(DeploymentAction.NewCreationAction());
                }
            }

            return creationActions;
        }

        // Checks that previously started deployments have finished starting up, transfer them into the Ready state
        private IEnumerable<DeploymentAction> GetUpdateActions(IEnumerable<Deployment> existingDeployments)
        {
            List<DeploymentAction> updateActions = new List<DeploymentAction>();
            var startingDeployments = existingDeployments.Where(d => d.Tag.Contains(STARTING_TAG));
            foreach (var startingDeployment in startingDeployments.Where(dpl => dpl.Status == Deployment.Types.Status.Running || dpl.Status == Deployment.Types.Status.Error))
            {
                if (startingDeployment.Status == Deployment.Types.Status.Error)
                {
                    startingDeployment.Tag.Add(COMPLETED_TAG);
                }
                else if (startingDeployment.Status == Deployment.Types.Status.Running)
                {
                    startingDeployment.Tag.Add(READY_TAG);
                }
                startingDeployment.Tag.Remove(STARTING_TAG);

                updateActions.Add(DeploymentAction.NewUpdateAction(startingDeployment));
            }

            return updateActions;
        }

        // Checks if any deployments have finished, and shuts them down.
        private IEnumerable<DeploymentAction> GetStopActions(IEnumerable<Deployment> existingDeployments)
        {
            List<DeploymentAction> stopActions = new List<DeploymentAction>();
            var completeDeployments = existingDeployments.Where(d => d.Tag.Contains(COMPLETED_TAG) && !d.Tag.Contains(STOPPING_TAG));
            var stoppingDeployments = existingDeployments.Where(d => d.Tag.Contains(STOPPING_TAG));
            if (completeDeployments.Any() || stoppingDeployments.Any())
            {
                Log.Logger.Information($"{completeDeployments.Count()} deployments to shut down. {stoppingDeployments.Count()} in progress.");
            }
            foreach (var completeDeployment in completeDeployments)
            {
                stopActions.Add(DeploymentAction.NewStopAction(completeDeployment));
            }

            return stopActions;
        }

        private void StartDeployment(string newDeploymentName)
        {
            Log.Logger.Information("Starting new deployment named {dplName}", newDeploymentName);
            var snapshotId = CreateSnapshotId(newDeploymentName);
            var launchConfig = GetLaunchConfig();

            var deployment = new Deployment
            {
                Name = newDeploymentName,
                ProjectName = _spatialProject,
                Description = "Launched by Deployment Pool",
                AssemblyId = assemblyName,
                LaunchConfig = launchConfig,
                StartingSnapshotId = snapshotId,
            };
            deployment.Tag.Add(STARTING_TAG);
            deployment.Tag.Add(matchType);
            deployment.WorkerConnectionCapacities.Add(
                new WorkerCapacity
                {
                    WorkerType = "External",
                    MaxCapacity = 10,
                }
            );

            var createDeploymentRequest = new CreateDeploymentRequest
            {
                Deployment = deployment
            };
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
                    Log.Logger.Information("Deployment {dplName} started succesfully", completed.Result.Name);
                }
                else
                {
                    Log.Logger.Information("Something went wrong starting deployment {dplName}", completed.Result.Name);
                }
            });
        }

        private void UpdateDeployment(Deployment dpl)
        {
            _deploymentServiceClient.UpdateDeployment(new UpdateDeploymentRequest
            {
                Deployment = dpl
            });
        }

        private void StopDeployment(Deployment deployment)
        {
            Log.Logger.Information("Stopping {dplName}", deployment.Name);
            // Set the stopping tag
            deployment.Tag.Add(STOPPING_TAG);
            deployment.Tag.Remove(READY_TAG);
            UpdateDeployment(deployment);

            // Stop the deployment
            var stopRequest = new StopDeploymentRequest
            {
                ProjectName = deployment.ProjectName,
                Id = deployment.Id
            };
            _deploymentServiceClient.StopDeployment(stopRequest);
        }

        private string CreateSnapshotId(string deploymentName)
        {
            var snapshot = File.ReadAllBytes(snapshotFilePath);
            string checksum;
            using (var md5 = MD5.Create())
            {
                checksum = Convert.ToBase64String(md5.ComputeHash(snapshot));
            }

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

            Log.Logger.Information("Uploaded new snapshot at Id {snapshotId}", response.Snapshot.Id);
            return response.Snapshot.Id;
        }

        private LaunchConfig GetLaunchConfig()
        {
            var jsonString = File.ReadAllText(launchConfigFilePath, Encoding.UTF8);
            var launchConfig = new LaunchConfig
            {
                ConfigJson = jsonString
            };
            return launchConfig;
        }

        public IEnumerable<Deployment> ListDeployments()
        {
            return _deploymentServiceClient
                .ListDeployments(new ListDeploymentsRequest
                {
                    ProjectName = _spatialProject,
                    PageSize = 50,
                    DeploymentStoppedStatusFilter = ListDeploymentsRequest.Types.DeploymentStoppedStatusFilter
                        .NotStoppedDeployments,
                    View = ViewType.Basic
                })
                .Where(d => d.Tag.Contains(matchType));
        }
    }
}