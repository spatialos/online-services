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
using Improbable.OnlineServices.Proto.Metadata;
using Improbable.SpatialOS.Deployment.V1Alpha1;
using Improbable.SpatialOS.Snapshot.V1Alpha1;
using Serilog;

namespace DeploymentPool
{
    public class DeploymentPool
    {
        public const string ReadinessKey = "readiness";
        public const string StartingTag = "starting";
        private const string ReadyTag = "ready";
        private const string StoppingTag = "stopping";
        private const string CompletedTag = "completed";

        private CancellationToken cancelToken;
        private readonly string matchType;
        private readonly string spatialProject;
        private readonly int minimumReadyDeployments;
        private readonly bool cleanup;
        private readonly PlatformInvoker platformInvoker;
        private readonly DeploymentServiceClient deploymentServiceClient;
        private readonly DeploymentMetadataService.DeploymentMetadataServiceClient metadataServiceClient;

        public DeploymentPool(
            DeploymentPoolArgs args,
            DeploymentServiceClient deploymentServiceClient,
            PlatformInvoker platformInvoker,
            DeploymentMetadataService.DeploymentMetadataServiceClient metadataServiceClient,
            CancellationToken token)
        {
            cancelToken = token;
            matchType = args.MatchType;
            spatialProject = args.SpatialProject;
            minimumReadyDeployments = args.MinimumReadyDeployments;
            cleanup = args.Cleanup;
            this.platformInvoker = platformInvoker;
            this.deploymentServiceClient = deploymentServiceClient;
            this.metadataServiceClient = metadataServiceClient;
        }

        public async Task Start()
        {
            await Run();
        }

        public void StopAll()
        {
            // Basic views do not include player information
            var stopActions = ListDeployments().Select(dpl =>
            {
                dpl.Tag.Add(StoppingTag);
                dpl.Tag.Remove(ReadyTag);
                dpl.Tag.Remove(StartingTag);
                return DeploymentAction.NewStopAction(dpl, StoppingTag);
            }).ToList();

            Log.Logger.Warning("Stopping all running {} deployments", matchType);
            platformInvoker.InvokeActions(stopActions);
            Log.Logger.Information("All deployments have been stopped.");
        }

        public async Task Run()
        {
            var retries = 0;
            while (!cancelToken.IsCancellationRequested)
            {
                try
                {
                    var matchDeployments = ListDeployments();
                    var annotatedDeployments = ListDeploymentReadiness(matchDeployments);
                    var actions = GetRequiredActions(annotatedDeployments);
                    platformInvoker.InvokeActions(actions);
                    await Task.Delay(TimeSpan.FromSeconds(10));
                    retries = 0;
                }
                catch (Exception e)
                {
                    // If we repeatedly catch exceptions, back off so we don't contribute to any downstream problems.
                    var retrySeconds = 2 ^ retries;
                    Log.Logger.Warning("Exception encountered during iteration: Retrying in {retrySeconds}s. Error was {e}", retrySeconds, e);
                    await Task.Delay(TimeSpan.FromSeconds(retrySeconds));
                    retries = Math.Min(retries + 1, 4);
                }
            }

            if (cleanup)
            {
                StopAll();
            }
            Log.Logger.Information("Shutdown signal received. Pool has stopped.");
        }

        public IEnumerable<DeploymentAction> GetRequiredActions(IEnumerable<(Deployment deployment, string readiness)> matchDeployments)
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

        // Checks for discrepencies between Running+Starting deployments and the requested minimum number.
        private IEnumerable<DeploymentAction> GetCreationActions(IEnumerable<(Deployment deployment, string readiness)> existingDeployments)
        {
            List<DeploymentAction> creationActions = new List<DeploymentAction>();
            var readyDeployments = existingDeployments.Count(d => d.readiness == ReadyTag);
            var startingDeployments = existingDeployments.Count(d => d.readiness == StartingTag);
            Reporter.ReportDeploymentsInReadyState(matchType, readyDeployments);
            Reporter.ReportDeploymentsInStartingState(matchType, startingDeployments);
            var availableDeployments = readyDeployments + startingDeployments;
            Log.Logger.Information(
                $"{readyDeployments}/{minimumReadyDeployments} deployments ready for use; {startingDeployments} starting up.");

            if (availableDeployments < minimumReadyDeployments)
            {
                var diff = minimumReadyDeployments - availableDeployments;
                Log.Logger.Information($"Missing {diff} deployments");

                for (int i = 0; i < diff; i++)
                {
                    creationActions.Add(DeploymentAction.NewCreationAction(StartingTag));
                }
            }

            return creationActions;
        }

        // Checks that previously started deployments have finished starting up, transfer them into the Ready state
        private IEnumerable<DeploymentAction> GetUpdateActions(IEnumerable<(Deployment deployment, string readiness)> existingDeployments)
        {
            return existingDeployments
                .Where(d => d.readiness == StartingTag)
                .Where(d => d.deployment.Status == Deployment.Types.Status.Running || d.deployment.Status == Deployment.Types.Status.Error)
                .Select(startingDeployment =>
                {
                    var newDeployment = startingDeployment.deployment.Clone();
                    var action = DeploymentAction.NewUpdateAction(newDeployment, null, StartingTag);

                    if (startingDeployment.deployment.Status == Deployment.Types.Status.Error)
                    {
                        newDeployment.Tag.Add(CompletedTag);
                        action.newReadiness = CompletedTag;
                    }
                    else if (startingDeployment.deployment.Status == Deployment.Types.Status.Running)
                    {
                        newDeployment.Tag.Add(ReadyTag);
                        action.newReadiness = ReadyTag;
                    }

                    newDeployment.Tag.Remove(StartingTag);

                    return action;
                }).ToList();
        }

        // Checks if any deployments have finished, and shuts them down.
        private IEnumerable<DeploymentAction> GetStopActions(IEnumerable<(Deployment deployment, string readiness)> existingDeployments)
        {
            var completedDeployments = existingDeployments.Where(d => d.readiness == CompletedTag);
            var stoppingDeployments = existingDeployments.Where(d => d.readiness == StoppingTag);
            if (completedDeployments.Any() || stoppingDeployments.Any())
            {
                Log.Logger.Information($"{completedDeployments.Count()} deployments to shut down. {stoppingDeployments.Count()} in progress.");
            }

            return completedDeployments.Select(completedDeployment =>
            {
                var newDeployment = completedDeployment.deployment.Clone();
                newDeployment.Tag.Remove(ReadyTag);
                newDeployment.Tag.Add(StoppingTag);
                return DeploymentAction.NewStopAction(newDeployment, StoppingTag, CompletedTag);
            });
        }

        public IEnumerable<Deployment> ListDeployments()
        {
            return deploymentServiceClient
                .ListDeployments(new ListDeploymentsRequest
                {
                    ProjectName = spatialProject,
                    PageSize = 50,
                    DeploymentStoppedStatusFilter = ListDeploymentsRequest.Types.DeploymentStoppedStatusFilter
                        .NotStoppedDeployments,
                    View = ViewType.Basic,
                    Filters =
                    {
                        new Filter
                        {
                            TagsPropertyFilter = new TagsPropertyFilter
                            {
                                Operator = TagsPropertyFilter.Types.Operator.Equal,
                                Tag = matchType
                            }
                        }
                    }
                });
        }

        public IEnumerable<(Deployment deployment, string readiness)> ListDeploymentReadiness(IEnumerable<Deployment> deployments)
        {
            return deployments.Select(deployment =>
            {
                try
                {
                    var metadata = metadataServiceClient.GetDeploymentMetadata(new GetDeploymentMetadataRequest()
                    {
                        DeploymentId = deployment.Id
                    });
                    metadata.Value.TryGetValue(ReadinessKey, out var readiness);
                    return (deployment, readiness);
                }
                catch (RpcException e)
                {
                    if (e.StatusCode == StatusCode.NotFound && deployment.Tag.Contains(StartingTag))
                    {
                        // Deployment is still starting up (but metadata not yet set).
                        return (deployment, StartingTag);
                    }
                    else
                    {
                        // Deployment is in a weird state or wasn't previously registered against the metadata service.
                        Log.Logger.Warning("Couldn't get metadata for deployment. Error: {err}", e.Message);
                        return (deployment, null);
                    }
                }
            }).Where(tuple => tuple.Item2 != null);
        }
    }
}
