using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using Improbable.OnlineServices.Proto.Metadata;
using Improbable.SpatialOS.Deployment.V1Beta1;
using Improbable.SpatialOS.Snapshot.V1Alpha1;
using Serilog;

namespace DeploymentPool
{
    public class PlatformInvoker
    {
        private readonly DeploymentServiceClient deploymentServiceClient;
        private readonly SnapshotServiceClient snapshotServiceClient;
        private readonly DeploymentMetadataService.DeploymentMetadataServiceClient metadataServiceClient;
        private readonly string deploymentNamePrefix;
        private readonly string launchConfigFilePath;
        private readonly string snapshotFilePath;
        private readonly string assemblyName;
        private readonly string spatialProject;
        private readonly string matchType;
        private int deploymentIndex = 1;

        public PlatformInvoker(DeploymentPoolArgs args,
            DeploymentServiceClient deploymentServiceClient,
            SnapshotServiceClient snapshotServiceClient,
            DeploymentMetadataService.DeploymentMetadataServiceClient metadataServiceClient)
        {
            deploymentNamePrefix = args.DeploymentNamePrefix + HumanNamer.GetRandomName(2, "_") + "_";
            launchConfigFilePath = args.LaunchConfigFilePath;
            snapshotFilePath = args.SnapshotFilePath;
            assemblyName = args.AssemblyName;
            spatialProject = args.SpatialProject;
            matchType = args.MatchType;
            this.deploymentServiceClient = deploymentServiceClient;
            this.snapshotServiceClient = snapshotServiceClient;
            this.metadataServiceClient = metadataServiceClient;
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
                        tasks[i] = Task.Run(() => StartDeployment(deploymentNamePrefix + deploymentIndex++, deploymentAction.newReadiness));
                        break;
                    case DeploymentAction.ActionType.Update:
                        tasks[i] = Task.Run(() => UpdateDeployment(deploymentAction.deployment, deploymentAction.newReadiness, deploymentAction.oldReadiness));
                        break;
                    case DeploymentAction.ActionType.Stop:
                        tasks[i] = Task.Run(() => StopDeployment(deploymentAction.deployment));
                        break;
                    default:
                        throw new Exception($"Unknown DeploymentAction {deploymentAction.actionType} encountered");
                }
            }

            Task.WaitAll(tasks);
        }


        private void StartDeployment(string newDeploymentName, string newDeploymentReadiness)
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

            var createDeploymentRequest = new CreateDeploymentRequest
            {
                DeploymentName = newDeploymentName,
                ProjectName = spatialProject,
                AssemblyName = assemblyName,
                LaunchConfig = launchConfig,
                StartingSnapshotId = snapshotId,
            };
            createDeploymentRequest.Tags.Add(DeploymentPool.StartingTag);
            createDeploymentRequest.Tags.Add(matchType);

            try
            {
                var startTime = DateTime.Now;
                Reporter.ReportDeploymentCreationRequest(matchType);
                var createOp = deploymentServiceClient.CreateDeployment(createDeploymentRequest);
                Task.Run(() =>
                {
                    var completed = createOp.PollUntilCompleted();
                    Reporter.ReportDeploymentCreationDuration(matchType, (DateTime.Now - startTime).TotalSeconds);
                    if (completed.IsCompleted)
                    {
                        metadataServiceClient.SetDeploymentMetadataEntry(new SetDeploymentMetadataEntryRequest
                        {
                            DeploymentId = completed.Result.Id.ToString(),
                            Key = DeploymentPool.ReadinessKey,
                            Value = newDeploymentReadiness,
                            Condition = new Condition
                            {
                                Function = Condition.Types.Function.NotExists,
                            },
                        });
                        Log.Logger.Information("Deployment {dplName} started successfully", completed.Result.DeploymentName);
                    }
                    else if (completed.IsFaulted)
                    {
                        Log.Logger.Error("Failed to start deployment {DplName}. Operation {opName}. Error {err}", createDeploymentRequest.DeploymentName, completed.Name, completed.Exception.Message);
                    }
                    else
                    {
                        Log.Logger.Error("Internal error starting deployment {dplName}. Operation {opName}. Error {err}", completed.Result.DeploymentName, completed.Name, completed.Exception.Message);
                    }
                });
            }
            catch (RpcException e)
            {
                Reporter.ReportDeploymentCreationFailure(matchType);
                Log.Logger.Error("Failed to start deployment creation. Error: {err}", e.Message);
            }
        }

        private void UpdateDeployment(Deployment dpl, string newReadiness = null, string oldReadiness = null)
        {
            try
            {
                Reporter.ReportDeploymentUpdateRequest(matchType);

                if (newReadiness != oldReadiness)
                {
                    // Update metadata before attempting the deployment update call - a value conflict here will raise
                    // an exception and thereby prevent the deployment being incorrectly updated. 
                    var request = new SetDeploymentMetadataEntryRequest
                    {
                        DeploymentId = dpl.Id.ToString(),
                        Key = DeploymentPool.ReadinessKey,
                        Value = newReadiness,
                        Condition = oldReadiness == null
                            ? null
                            : new Condition
                            {
                                Function = Condition.Types.Function.Equal,
                                Payload = oldReadiness
                            }
                    };

                    metadataServiceClient.SetDeploymentMetadataEntryAsync(request);
                }

                var req = new SetDeploymentTagsRequest
                {
                    DeploymentId = dpl.Id
                };
                req.Tags.AddRange(dpl.Tags);
                deploymentServiceClient.SetDeploymentTags(req);
            }
            catch (RpcException e)
            {
                Reporter.ReportDeploymentUpdateFailure(matchType);
                Log.Logger.Error("Failed to update deployment {dplName}. Error: {err}", dpl.DeploymentName, e.Message);
            }
        }

        private void StopDeployment(Deployment deployment)
        {
            Log.Logger.Information("Stopping {dplName}", deployment.DeploymentName);
            // Update any tag changes
            UpdateDeployment(deployment);

            // Stop the deployment
            var deleteDeploymentRequest = new DeleteDeploymentRequest
            {
                Id = deployment.Id
            };
            try
            {
                var startTime = DateTime.Now;
                Reporter.ReportDeploymentStopRequest(matchType);
                var deleteOp = deploymentServiceClient.DeleteDeployment(deleteDeploymentRequest);
                Task.Run(() =>
                {
                    var completed = deleteOp.PollUntilCompleted();
                    Reporter.ReportDeploymentStopDuration(matchType, (DateTime.Now - startTime).TotalSeconds);
                    metadataServiceClient.DeleteDeploymentMetadata(new DeleteDeploymentMetadataRequest
                    {
                        DeploymentId = deployment.Id.ToString()
                    });
                    if (completed.IsCompleted)
                    {
                        Log.Logger.Information("Deployment {dplName} stopped successfully", completed.Result.DeploymentName);
                    }
                    else if (completed.IsFaulted)
                    {
                        Log.Logger.Error("Failed to stop deployment {DplName}. Operation {opName}. Error {err}", deployment.DeploymentName, completed.Name, completed.Exception.Message);
                    }
                    else
                    {
                        Log.Logger.Error("Internal error stopping deployment {dplName}. Operation {opName}. Error {err}", completed.Result.DeploymentName, completed.Name, completed.Exception.Message);
                    }
                });
            }
            catch (RpcException e)
            {
                Reporter.ReportDeploymentStopFailure(matchType);
                Log.Logger.Warning("Failed to start deployment deletion. Error: {err}", e.Message);
            }
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
