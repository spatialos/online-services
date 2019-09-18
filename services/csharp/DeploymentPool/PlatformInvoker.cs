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
using Improbable.OnlineServices.Common.Analytics;
using Improbable.SpatialOS.Deployment.V1Alpha1;
using Improbable.SpatialOS.Snapshot.V1Alpha1;
using Serilog;

namespace DeploymentPool
{
    public class PlatformInvoker
    {
        
        private readonly DeploymentServiceClient deploymentServiceClient;
        private readonly SnapshotServiceClient snapshotServiceClient;
        private readonly AnalyticsSenderClassWrapper _analytics;
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
            IAnalyticsSender analytics = null)
        {
            deploymentNamePrefix = args.DeploymentNamePrefix + HumanNamer.GetRandomName(2, "_") + "_";
            launchConfigFilePath = args.LaunchConfigFilePath;
            snapshotFilePath = args.SnapshotFilePath;
            assemblyName = args.AssemblyName;
            spatialProject = args.SpatialProject;
            matchType = args.MatchType;
            this.deploymentServiceClient = deploymentServiceClient;
            this.snapshotServiceClient = snapshotServiceClient;
            _analytics = (analytics ?? new NullAnalyticsSender()).WithEventClass("deployment");
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
                        tasks[i] = Task.Run(() => StartDeployment(deploymentNamePrefix + deploymentIndex++));
                        break;
                    case DeploymentAction.ActionType.Update:
                        tasks[i] = Task.Run(() => UpdateDeployment(deploymentAction.deployment));
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

            try
            {
                var startTime = DateTime.Now;
                Reporter.ReportDeploymentCreationRequest(matchType);
                var createOp = deploymentServiceClient.CreateDeployment(createDeploymentRequest);
                _analytics.Send("deployment_started", new Dictionary<string, string>
                {
                    { "spatialProjectId", createDeploymentRequest.Deployment.ProjectName },
                    { "deploymentName", createDeploymentRequest.Deployment.Name }
                    // Todo: need a deploymentId
                });
                Task.Run(() =>
                {
                    var completed = createOp.PollUntilCompleted();
                    Reporter.ReportDeploymentCreationDuration(matchType, (DateTime.Now - startTime).TotalSeconds);
                    if (completed.IsCompleted)
                    {
                        Log.Logger.Information("Deployment {dplName} started successfully", completed.Result.Name);
                        _analytics.Send("deployment_ready", new Dictionary<string, string>
                        {
                            { "spatialProjectId", completed.Result.ProjectName },
                            { "deploymentName", completed.Result.Name },
                            { "deploymentId", completed.Result.Id },
                            { "startingSnapshotId", completed.Result.StartingSnapshotId },
                            { "assemblyId", completed.Result.AssemblyId },
                            { "clusterCode", completed.Result.ClusterCode },
                            { "regionCode", completed.Result.RegionCode },
                            { "runtimeVersion", completed.Result.RuntimeVersion }
                        });
                    }
                    else if (completed.IsFaulted)
                    {
                        Log.Logger.Error("Failed to start deployment {DplName}. Operation {opName}. Error {err}", createDeploymentRequest.Deployment.Name, completed.Name, completed.Exception.Message);
                        _analytics.Send("deployment_error", new Dictionary<string, string>
                        {
                            { "spatialProjectId", createDeploymentRequest.Deployment.ProjectName },
                            { "deploymentName", createDeploymentRequest.Deployment.Name },
                            { "operation", completed.Name },
                            { "errorMessage", completed.Exception.Message }
                        });
                    }
                    else
                    {
                        Log.Logger.Error("Internal error starting deployment {dplName}. Operation {opName}. Error {err}", completed.Result.Name, completed.Name, completed.Exception.Message);
                        _analytics.Send("deployment_error", new Dictionary<string, string>
                        {
                            { "spatialProjectId", completed.Result.ProjectName },
                            { "deploymentName", completed.Result.Name },
                            { "deploymentId", completed.Result.Id },
                            { "operation", completed.Name },
                            { "errorMessage", completed.Exception.Message }
                        });
                    }
                });
            }
            catch (RpcException e)
            {
                Reporter.ReportDeploymentCreationFailure(matchType);
                Log.Logger.Error("Failed to start deployment creation. Error: {err}", e.Message);
            }
        }

        private void UpdateDeployment(Deployment dpl)
        {
            try
            {
                Reporter.ReportDeploymentUpdateRequest(matchType);
                deploymentServiceClient.UpdateDeployment(new UpdateDeploymentRequest
                {
                    Deployment = dpl
                });

            }
            catch (RpcException e)
            {
                Reporter.ReportDeploymentUpdateFailure(matchType);
                Log.Logger.Error("Failed to update deployment {dplName}. Error: {err}", dpl.Name, e.Message);
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
            try
            {
                var startTime = DateTime.Now;
                Reporter.ReportDeploymentStopRequest(matchType);
                var deleteOp = deploymentServiceClient.DeleteDeployment(deleteDeploymentRequest);
                _analytics.Send("deployment_stopping", new Dictionary<string, string>
                {
                    { "spatialProjectId", deployment.ProjectName },
                    { "deploymentName", deployment.Name },
                    { "deploymentId", deployment.Id }
                });
                Task.Run(() =>
                {
                    var completed = deleteOp.PollUntilCompleted();
                    Reporter.ReportDeploymentStopDuration(matchType, (DateTime.Now - startTime).TotalSeconds);
                    if (completed.IsCompleted)
                    {
                        Log.Logger.Information("Deployment {dplName} stopped succesfully", completed.Result.Name);
                        _analytics.Send("deployment_stopped", new Dictionary<string, string>
                        {
                            { "spatialProjectId", completed.Result.ProjectName },
                            { "deploymentName", completed.Result.Name },
                            { "deploymentId", completed.Result.Id }
                        });
                    }
                    else if (completed.IsFaulted)
                    {
                        Log.Logger.Error("Failed to stop deployment {DplName}. Operation {opName}. Error {err}", deployment.Name, completed.Name, completed.Exception.Message);
                        _analytics.Send("deployment_error", new Dictionary<string, string>
                        {
                            { "spatialProjectId", deployment.ProjectName },
                            { "deploymentName", deployment.Name },
                            { "operation", completed.Name },
                            { "errorMessage", completed.Exception.Message }
                        });
                    }
                    else
                    {
                        Log.Logger.Error("Internal error stopping deployment {dplName}. Operation {opName}. Error {err}", completed.Result.Name, completed.Name, completed.Exception.Message);
                        _analytics.Send("deployment_error", new Dictionary<string, string>
                        {
                            { "spatialProjectId", completed.Result.ProjectName },
                            { "deploymentName", completed.Result.Name },
                            { "deploymentId", completed.Result.Id },
                            { "operation", completed.Name },
                            { "errorMessage", completed.Exception.Message }
                        });
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
