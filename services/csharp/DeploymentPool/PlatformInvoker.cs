using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Google.LongRunning;
using Google.Type;
using Grpc.Core;
using Improbable.SpatialOS.Deployment.V1Alpha1;
using Improbable.SpatialOS.Snapshot.V1Alpha1;
using Serilog;

namespace DeploymentPool
{
    public class PlatformInvoker
    {
        private readonly DeploymentServiceClient deploymentServiceClient;
        private readonly SnapshotServiceClient snapshotServiceClient;
        private readonly string deploymentNamePrefix;
        private readonly string launchConfigFilePath;
        private readonly string snapshotFilePath;
        private readonly string assemblyName;
        private readonly string spatialProject;
        private readonly string matchType;
        private int deploymentCounter = 1;

        public PlatformInvoker(DeploymentPoolArgs args,
            DeploymentServiceClient deploymentServiceClient,
            SnapshotServiceClient snapshotServiceClient)
        {
            deploymentNamePrefix = args.DeploymentNamePrefix + HumanNamer.GetRandomName(2, "_") + "_";
            launchConfigFilePath = args.LaunchConfigFilePath;
            snapshotFilePath = args.SnapshotFilePath;
            assemblyName = args.AssemblyName;
            spatialProject = args.SpatialProject;
            matchType = args.MatchType;
            this.deploymentServiceClient = deploymentServiceClient;
            this.snapshotServiceClient = snapshotServiceClient;
        }

        public void InvokeActions(IEnumerable<DeploymentAction> actionToTake)
        {
            var actions = actionToTake.ToList();
            var tasks = new Task[actions.Count];
            for (int i = 0; i < actions.Count; i++)
            {
                var deploymentAction = actions[i];
                switch (deploymentAction.actionType)
                {
                    case DeploymentAction.ActionType.Create:
                        tasks[i] = Task.Run(() => StartDeployment(deploymentNamePrefix + deploymentCounter++));
                        break;
                    case DeploymentAction.ActionType.Update:
                        tasks[i] = Task.Run(() => UpdateDeployment(deploymentAction.deployment));
                        break;
                    case DeploymentAction.ActionType.Stop:
                        tasks[i] = Task.Run(() => StopDeployment(deploymentAction.deployment));
                        break;
                    default:
                        throw new Exception("Unknown type encountered!");
                }
            }

            Task.WaitAll(tasks);
        }


        private void StartDeployment(string newDeploymentName)
        {
            Log.Logger.Information("Starting new deployment named {dplName}", newDeploymentName);
            string snapshotId;
            try
            {
                snapshotId = CreateSnapshotId(newDeploymentName);
            }
            catch (RpcException e)
            {
                if (e.StatusCode == StatusCode.ResourceExhausted)
                {
                    Log.Logger.Warning("Resource exhausted creating snapshot: {err}", e.Message);
                    return;
                }

                throw e;
            }
            var launchConfig = GetLaunchConfig();

            var deployment = new Deployment
            {
                Name = newDeploymentName,
                ProjectName = spatialProject,
                Description = "Launched by Deployment Pool",
                AssemblyId = assemblyName,
                LaunchConfig = launchConfig,
                StartingSnapshotId = snapshotId,
            };
            deployment.Tag.Add(DeploymentPool.StartingTag);
            deployment.Tag.Add(matchType);

            var createDeploymentRequest = new CreateDeploymentRequest
            {
                Deployment = deployment
            };

            Operation<Deployment, CreateDeploymentMetadata> createOp;
            try
            {
                createOp = deploymentServiceClient.CreateDeployment(createDeploymentRequest);
            }
            catch (RpcException e)
            {
                Log.Logger.Warning("Failed to create deployment flow. Error: {err}", e.Message);
                return;
            }
            Task.Run(() =>
            {
                var completed = createOp.PollUntilCompleted();
                if (completed.IsFaulted)
                {
                    Log.Logger.Information("Failed to start deployment {DplName}. Operation {opName}. Error {err}", createDeploymentRequest.Deployment.Name, completed.Name, completed.Exception.Message);
                }
                else if (completed.IsCompleted)
                {
                    Log.Logger.Information("Deployment {dplName} started succesfully", completed.Result.Name);
                }
                else
                {
                    Log.Logger.Information("Internal error starting deployment {dplName}. Operation {opName}. Error {err}", completed.Result.Name, completed.Name, completed.Exception.Message);
                }
            });
        }

        private void UpdateDeployment(Deployment dpl)
        {
            try
            {
                deploymentServiceClient.UpdateDeployment(new UpdateDeploymentRequest
                {
                    Deployment = dpl
                });

            }
            catch (RpcException e)
            {
                Log.Logger.Warning("Failed to update deployment {dplName}. Error: {err}", dpl.Name, e.Message);
            }
        }

        private void StopDeployment(Deployment deployment)
        {
            Log.Logger.Information("Stopping {dplName}", deployment.Name);
            // Update any tag changes
            UpdateDeployment(deployment);

            // Stop the deployment
            var deleteDeploymentRequest = new DeleteDeploymentRequest
            {
                Id = deployment.Id
            };
            Operation<Deployment, DeleteDeploymentMetadata> deleteOp;
            try
            {
                deleteOp = deploymentServiceClient.DeleteDeployment(deleteDeploymentRequest);
            }
            catch (RpcException e)
            {
                Log.Logger.Warning("Failed to start DeleteDeployment flow. Error: {err}", e.Message);
                return;
            }
            Task.Run(() =>
            {
                var completed = deleteOp.PollUntilCompleted();
                if (completed.IsFaulted)
                {
                    Log.Logger.Warning("Failed to stop deployment {DplName}. Operation {opName}. Error {err}", deployment.Name, completed.Name, completed.Exception.Message);
                }
                else if (completed.IsCompleted)
                {
                    Log.Logger.Information("Deployment {dplName} stopped succesfully", completed.Result.Name);
                }
                else
                {
                    Log.Logger.Warning("Internal error stopping deployment {dplName}. Operation {opName}. Error {err}", completed.Result.Name, completed.Name, completed.Exception.Message);
                }
            });
        }

        private string CreateSnapshotId(string deploymentName)
        {
            var snapshot = File.ReadAllBytes(snapshotFilePath);
            string checksum;
            using (var md5 = MD5.Create())
            {
                checksum = Convert.ToBase64String(md5.ComputeHash(snapshot));
            }

            var response = snapshotServiceClient.UploadSnapshot(new UploadSnapshotRequest
            {
                Snapshot = new Snapshot
                {
                    ProjectName = spatialProject,
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

            snapshotServiceClient.ConfirmUpload(new ConfirmUploadRequest
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
    }
}
