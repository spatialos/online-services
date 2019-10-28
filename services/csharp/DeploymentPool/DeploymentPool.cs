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
using Improbable.OnlineServices.Common.Analytics;
using Improbable.SpatialOS.Deployment.V1Alpha1;
using Improbable.SpatialOS.Snapshot.V1Alpha1;
using Serilog;

namespace DeploymentPool
{
    public class DeploymentPool
    {
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
        private readonly AnalyticsSenderClassWrapper _analytics;

        public DeploymentPool(
            DeploymentPoolArgs args,
            DeploymentServiceClient deploymentServiceClient,
            PlatformInvoker platformInvoker,
            CancellationToken token,
            IAnalyticsSender analytics = null)
        {
            cancelToken = token;
            matchType = args.MatchType;
            spatialProject = args.SpatialProject;
            minimumReadyDeployments = args.MinimumReadyDeployments;
            cleanup = args.Cleanup;
            this.platformInvoker = platformInvoker;
            this.deploymentServiceClient = deploymentServiceClient;
            _analytics = (analytics ?? new NullAnalyticsSender()).WithEventClass("deployment");
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
                return DeploymentAction.NewStopAction(dpl);
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
                    var actions = GetRequiredActions(matchDeployments);
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

        // Checks for discrepencies between Running+Starting deployments and the requested minimum number.
        private IEnumerable<DeploymentAction> GetCreationActions(IEnumerable<Deployment> existingDeployments)
        {
            List<DeploymentAction> creationActions = new List<DeploymentAction>();
            var readyDeployments = existingDeployments.Count(d => d.Tag.Contains(ReadyTag));
            var startingDeployments = existingDeployments.Count(d => d.Tag.Contains(StartingTag));
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
                    creationActions.Add(DeploymentAction.NewCreationAction());
                }
            }

            return creationActions;
        }

        // Checks that previously started deployments have finished starting up, transfer them into the Ready state
        private IEnumerable<DeploymentAction> GetUpdateActions(IEnumerable<Deployment> existingDeployments)
        {
            return existingDeployments
                .Where(d => d.Tag.Contains(StartingTag))
                .Where(dpl => dpl.Status == Deployment.Types.Status.Running || dpl.Status == Deployment.Types.Status.Error)
                .Select(startingDeployment =>
                {
                    var newDeployment = startingDeployment.Clone();
                    if (startingDeployment.Status == Deployment.Types.Status.Error)
                    {
                        newDeployment.Tag.Add(CompletedTag);
                    }
                    else if (startingDeployment.Status == Deployment.Types.Status.Running)
                    {
                        newDeployment.Tag.Add(ReadyTag);
                    }

                    newDeployment.Tag.Remove(StartingTag);

                    return DeploymentAction.NewUpdateAction(newDeployment);
                }).ToList();
        }

        // Checks if any deployments have finished, and shuts them down.
        private IEnumerable<DeploymentAction> GetStopActions(IEnumerable<Deployment> existingDeployments)
        {
            var completedDeployments = existingDeployments.Where(d => d.Tag.Contains(CompletedTag) && !d.Tag.Contains(StoppingTag));
            var stoppingDeployments = existingDeployments.Where(d => d.Tag.Contains(StoppingTag));
            if (completedDeployments.Any() || stoppingDeployments.Any())
            {
                Log.Logger.Information($"{completedDeployments.Count()} deployments to shut down. {stoppingDeployments.Count()} in progress.");
            }

            return completedDeployments.Select(completedDeployment =>
            {
                var newDeployment = completedDeployment.Clone();
                newDeployment.Tag.Remove(ReadyTag);
                newDeployment.Tag.Add(StoppingTag);
                _analytics.Send("deployment_completed", new Dictionary<string, string>
                {
                    { "spatialProjectId", completedDeployment.ProjectName },
                    { "deploymentName", completedDeployment.Name },
                    { "deploymentId", completedDeployment.Id }
                    });
                return DeploymentAction.NewStopAction(newDeployment);
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
    }
}
