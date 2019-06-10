using System;
using System.Linq;
using System.Threading;
using Google.Protobuf.Collections;
using Grpc.Core;
using Improbable.OnlineServices.Proto.Gateway;
using Improbable.SpatialOS.Deployment.V1Alpha1;

namespace Improbable.OnlineServices.SampleMatcher
{
    public class Matcher : Improbable.OnlineServices.Base.Matcher.Matcher
    {
        private const int TickMs = 200;
        private readonly string _project;
        private readonly string ReadyTag = "ready"; // This should be the same tag a DeploymentPool looks for.
        private readonly string InUseTag = "in-use";
        private RepeatedField<WaitingParty> _waitingParties;

        public Matcher()
        {
            _project = Environment.GetEnvironmentVariable("SPATIAL_PROJECT");
            _waitingParties = new RepeatedField<WaitingParty>();
        }

        protected override void DoMatch(GatewayInternalService.GatewayInternalServiceClient gatewayClient,
            DeploymentServiceClient deploymentServiceClient)
        {
            try
            {
                var resp = gatewayClient.PopWaitingParties(new PopWaitingPartiesRequest
                {
                    Type = "match",
                    NumParties = 1
                });
                _waitingParties = resp.Parties;
                Console.WriteLine($"Fetched {resp.Parties.Count} from gateway");

                foreach (var party in _waitingParties)
                {
                    Console.WriteLine("Attempting to match a retrieved party.");
                    if (party.Metadata.TryGetValue("deployment_tag", out var deploymentTag))
                    {
                        var deployment = GetDeploymentWithTag(deploymentServiceClient, deploymentTag);
                        if (deployment != null)
                        {
                            var assignRequest = new AssignDeploymentsRequest();
                            Console.WriteLine("Found a deployment, assigning it to the party.");
                            assignRequest.Assignments.Add(new Assignment
                            {
                                DeploymentId = deployment.Id,
                                DeploymentName = deployment.Name,
                                Result = Assignment.Types.Result.Matched,
                                Party = party.Party
                            });
                            MarkDeploymentAsInUse(deploymentServiceClient, deployment);
                            gatewayClient.AssignDeployments(assignRequest);
                            _waitingParties.Remove(party);
                        }
                        else
                        {
                            Console.WriteLine(
                                $"Unable to find a deployment with tag {deploymentTag} in project {_project}");
                            Console.WriteLine("Erroring the request");
                            AssignPartyAsError(gatewayClient, party);
                            _waitingParties.Remove(party);
                        }
                    }
                    else
                    {
                        Console.WriteLine("No deployment_tag in metadata - erroring the request");
                        AssignPartyAsError(gatewayClient, party);
                        _waitingParties.Remove(party);
                    }
                }
            }
            catch (RpcException e)
            {
                if (e.StatusCode != StatusCode.ResourceExhausted && e.StatusCode != StatusCode.Unavailable)
                {
                    throw;
                }

                /* Unable to get the requested number of parties - ignore. */
                Console.WriteLine("No parties available.");
                Thread.Sleep(TickMs);
            }
        }

        private void AssignPartyAsError(GatewayInternalService.GatewayInternalServiceClient gatewayClient,
            WaitingParty party)
        {
            var assignRequest = new AssignDeploymentsRequest();
            assignRequest.Assignments.Add(new Assignment
            {
                Result = Assignment.Types.Result.Error,
                Party = party.Party
            });
            gatewayClient.AssignDeployments(assignRequest);
        }

        private Deployment GetDeploymentWithTag(DeploymentServiceClient deploymentServiceClient, string tag)
        {
            return deploymentServiceClient
                .ListDeployments(new ListDeploymentsRequest
                {
                    ProjectName = _project,
                    DeploymentStoppedStatusFilter = ListDeploymentsRequest.Types.DeploymentStoppedStatusFilter.NotStoppedDeployments,
                    View = ViewType.Basic
                })
                .TakeWhile(d => d.Status == Deployment.Types.Status.Running)
                .FirstOrDefault(d => d.Tag.Contains(tag) && d.Tag.Contains(ReadyTag));
        }

        protected override void DoShutdown(GatewayInternalService.GatewayInternalServiceClient gatewayClient,
            DeploymentServiceClient deploymentServiceClient)
        {
            /* Relinquish parties if the matcher shuts down. */
            foreach (var party in _waitingParties)
            {
                AssignPartyAsError(gatewayClient, party);
            }
        }

        private void MarkDeploymentAsInUse(DeploymentServiceClient dplClient, Deployment dpl)
        {
            dpl.Tag.Remove(ReadyTag);
            dpl.Tag.Add(InUseTag);
            var req = new UpdateDeploymentRequest { Deployment = dpl };
            dplClient.UpdateDeployment(req);
        }
    }
}