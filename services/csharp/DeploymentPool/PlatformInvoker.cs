using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using Improbable.OnlineServices.Common.Analytics;
using Improbable.SpatialOS.Deployment.V1Alpha1;
using Improbable.SpatialOS.Snapshot.V1Alpha1;
using Serilog;

namespace DeploymentPool
{
    public class PlatformInvoker
    {

        private readonly DeploymentServiceClient _deploymentServiceClient;
        private readonly SnapshotServiceClient _snapshotServiceClient;
        private readonly AnalyticsSenderClassWrapper _analytics;
        private readonly IEnumerable<string> _tags;
        private readonly string _deploymentNamePrefix;
        private readonly string _launchConfigFilePath;
        private readonly string _snapshotFilePath;
        private readonly string _assemblyName;
        private readonly string _spatialProject;
        private readonly string _selectorTag;
        private readonly string _runtimeVersion;
        private readonly string _clusterCode;
        private int _deploymentIndex = 1;

        public PlatformInvoker(DeploymentPoolArgs args,
            DeploymentServiceClient deploymentServiceClient,
            SnapshotServiceClient snapshotServiceClient,
            IAnalyticsSender analytics = null)
        {
            _tags = args.Tags;
            _deploymentNamePrefix = args.DeploymentNamePrefix + HumanNamer.GetRandomName(2, "_") + "_";
            _launchConfigFilePath = args.LaunchConfigFilePath;
            _snapshotFilePath = args.SnapshotFilePath;
            _assemblyName = args.AssemblyName;
            _spatialProject = args.SpatialProject;
            _selectorTag = args.SelectorTag;
            _runtimeVersion = args.RuntimeVersion;
            _clusterCode = args.ClusterCode;
            _deploymentServiceClient = deploymentServiceClient;
            _snapshotServiceClient = snapshotServiceClient;
            _analytics = (analytics ?? new NullAnalyticsSender()).WithEventClass("deployment");
        }

        public void InvokeActions(IEnumerable<DeploymentAction> actionToTake)
        {
            var blockingTasks = new List<Task>();
            foreach (var deploymentAction in actionToTake)
            {
                Task task;
                switch (deploymentAction.actionType)
                {
                    case DeploymentAction.ActionType.Create:
                        task = Task.Run(() => StartDeployment(_deploymentNamePrefix + _deploymentIndex++));
                        break;
                    case DeploymentAction.ActionType.Update:
                        task = Task.Run(() => UpdateDeployment(deploymentAction.deployment));
                        break;
                    case DeploymentAction.ActionType.Stop:
                        task = Task.Run(() => StopDeployment(deploymentAction.deployment));
                        break;
                    default:
                        throw new Exception($"Unknown DeploymentAction {deploymentAction.actionType} encountered");
                }

                if (deploymentAction.Blocking)
                {
                    blockingTasks.Add(task);
                }
            }

            Task.WaitAll(blockingTasks.ToArray());
        }


        private async Task StartDeployment(string newDeploymentName)
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

                throw;
            }
            var launchConfig = GetLaunchConfig();

            var deployment = new Deployment
            {
                Name = newDeploymentName,
                ProjectName = _spatialProject,
                Description = "Launched by Deployment Pool",
                AssemblyId = _assemblyName,
                LaunchConfig = launchConfig,
                StartingSnapshotId = snapshotId,
                RuntimeVersion = _runtimeVersion,
                ClusterCode = _clusterCode
            };
            deployment.Tag.Add(DeploymentPool.StartingTag);
            deployment.Tag.Add(_selectorTag);
            deployment.Tag.AddRange(_tags);

            var createDeploymentRequest = new CreateDeploymentRequest
            {
                Deployment = deployment
            };

            try
            {
                var startTime = DateTime.Now;
                Reporter.ReportDeploymentCreationRequest(_selectorTag);
                var createOp = await _deploymentServiceClient.CreateDeploymentAsync(createDeploymentRequest);
                _analytics.Send("deployment_started", new Dictionary<string, string>
                {
                    { "spatialProjectId", createDeploymentRequest.Deployment.ProjectName },
                    { "deploymentName", createDeploymentRequest.Deployment.Name }
                    // Todo: need a deploymentId
                });
                var completed = await createOp.PollUntilCompletedAsync();
                Reporter.ReportDeploymentCreationDuration(_selectorTag, (DateTime.Now - startTime).TotalSeconds);
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
            }
            catch (RpcException e)
            {
                Reporter.ReportDeploymentCreationFailure(_selectorTag);
                Log.Logger.Error("Failed to start deployment creation. Error: {err}", e.Message);
            }
        }

        private async Task UpdateDeployment(Deployment dpl)
        {
            try
            {
                Reporter.ReportDeploymentUpdateRequest(_selectorTag);
                await _deploymentServiceClient.UpdateDeploymentAsync(new UpdateDeploymentRequest
                {
                    Deployment = dpl
                });

            }
            catch (RpcException e)
            {
                Reporter.ReportDeploymentUpdateFailure(_selectorTag);
                Log.Logger.Error("Failed to update deployment {dplName}. Error: {err}", dpl.Name, e.Message);
            }
        }

        private async Task StopDeployment(Deployment deployment)
        {
            Log.Logger.Information("Stopping {dplName}", deployment.Name);
            // Update any tag changes
            await UpdateDeployment(deployment);

            // Stop the deployment
            var deleteDeploymentRequest = new DeleteDeploymentRequest
            {
                Id = deployment.Id
            };
            try
            {
                var startTime = DateTime.Now;
                Reporter.ReportDeploymentStopRequest(_selectorTag);
                var deleteOp = await _deploymentServiceClient.DeleteDeploymentAsync(deleteDeploymentRequest);
                _analytics.Send("deployment_stopping", new Dictionary<string, string>
                {
                    { "spatialProjectId", deployment.ProjectName },
                    { "deploymentName", deployment.Name },
                    { "deploymentId", deployment.Id }
                });
                var completed = await deleteOp.PollUntilCompletedAsync();
                Reporter.ReportDeploymentStopDuration(_selectorTag, (DateTime.Now - startTime).TotalSeconds);
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
            }
            catch (Exception e)
            {
                Reporter.ReportDeploymentStopFailure(_selectorTag);
                Log.Logger.Warning("Failed to start deployment deletion. Error: {err}", e.Message);
            }
        }

        private string CreateSnapshotId(string deploymentName)
        {
            if (string.IsNullOrWhiteSpace(_snapshotFilePath))
            {
                Log.Information("No snapshot path was provided - assuming default snapshot.");
                return "";
            }
            var snapshot = File.ReadAllBytes(_snapshotFilePath);
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
            var jsonString = File.ReadAllText(_launchConfigFilePath, Encoding.UTF8);
            var launchConfig = new LaunchConfig
            {
                ConfigJson = jsonString
            };
            return launchConfig;
        }
    }
}
