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
    public class DeploymentPool
    {
        public const string READY_TAG = "ready";
        public const string STARTING_TAG = "starting";
        public const string STOPPING_TAG = "stopping";
        public const string COMPLETED_TAG = "completed";

        private CancellationToken cancelToken;
        private readonly string matchType;
        private readonly string spatialProject;
        private readonly int minimumReadyDeployments;
        private readonly PlatformInvoker platformInvoker;
        private readonly DeploymentServiceClient deploymentServiceClient;

        public DeploymentPool(
            DeploymentPoolArgs args,
            DeploymentServiceClient deploymentServiceClient,
            PlatformInvoker platformInvoker,
            CancellationToken token)
        {
            cancelToken = token;
            matchType = args.MatchType;
            spatialProject = args.SpatialProject;
            minimumReadyDeployments = args.MinimumReadyDeployments;
            this.platformInvoker = platformInvoker;
            this.deploymentServiceClient = deploymentServiceClient;
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
                dpl.Tag.Add(STOPPING_TAG);
                dpl.Tag.Remove(READY_TAG);
                return DeploymentAction.NewStopAction(dpl);
            }).ToList();

            Log.Logger.Warning("Stopping all running {} deployments", matchType);
            platformInvoker.InvokeActions(stopActions);
            Log.Logger.Information("All deployments have been stopped.");
        }

        public async Task Run()
        {
            while (!cancelToken.IsCancellationRequested)
            {
                var matchDeployments = ListDeployments();
                var actions = GetRequiredActions(matchDeployments);
                platformInvoker.InvokeActions(actions);
                await Task.Delay(TimeSpan.FromSeconds(10));
            }

            // TODO: Remove this once everything works: its just a clean up step
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
            return existingDeployments
                .Where(d => d.Tag.Contains(STARTING_TAG))
                .Where(dpl => dpl.Status == Deployment.Types.Status.Running || dpl.Status == Deployment.Types.Status.Error)
                .Select(startingDeployment =>
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

                    return DeploymentAction.NewUpdateAction(startingDeployment);
                }).ToList();
        }

        // Checks if any deployments have finished, and shuts them down.
        private IEnumerable<DeploymentAction> GetStopActions(IEnumerable<Deployment> existingDeployments)
        {
            var completedDeployments = existingDeployments.Where(d => d.Tag.Contains(COMPLETED_TAG) && !d.Tag.Contains(STOPPING_TAG));
            var stoppingDeployments = existingDeployments.Where(d => d.Tag.Contains(STOPPING_TAG));
            if (completedDeployments.Any() || stoppingDeployments.Any())
            {
                Log.Logger.Information($"{completedDeployments.Count()} deployments to shut down. {stoppingDeployments.Count()} in progress.");
            }

            return completedDeployments.Select(completedDeployment =>
            {
                completedDeployment.Tag.Remove(READY_TAG);
                completedDeployment.Tag.Add(STOPPING_TAG);
                return DeploymentAction.NewStopAction(completedDeployment);
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
                    View = ViewType.Basic
                })
                .Where(d => d.Tag.Contains(matchType));
        }
    }
}