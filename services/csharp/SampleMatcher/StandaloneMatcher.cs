using System;
using System.Linq;
using System.Threading;
using Grpc.Core;
using Improbable.OnlineServices.Proto.Gateway;
using Improbable.OnlineServices.Proto.Metadata;
using Improbable.SpatialOS.Deployment.V1Beta1;

namespace Improbable.OnlineServices.SampleMatcher
{
    public class StandaloneMatcher : Base.Matcher.Matcher
    {
        private const int TickMs = 200;
        private const string DefaultMatchTag = "match";
        private readonly string _project;
        private readonly string _tag;

        public StandaloneMatcher()
        {
            _project = Environment.GetEnvironmentVariable("SPATIAL_PROJECT");
            _tag = Environment.GetEnvironmentVariable("MATCH_TAG") ?? DefaultMatchTag;
        }

        // Invalid metadata client discarded, safe as gRPC clients only error on access.
        protected override void DoMatch(GatewayInternalService.GatewayInternalServiceClient gatewayClient,
            DeploymentServiceClient deploymentServiceClient,
            DeploymentMetadataService.DeploymentMetadataServiceClient _)
        {
            try
            {
                var resp = gatewayClient.PopWaitingParties(new PopWaitingPartiesRequest
                {
                    Type = _tag,
                    NumParties = 1
                });
                Console.WriteLine($"Fetched {resp.Parties.Count} from gateway");

                foreach (var party in resp.Parties)
                {
                    Console.WriteLine("Attempting to match a retrieved party.");
                    var deployment = GetReadyDeploymentWithTag(deploymentServiceClient, _tag);
                    if (deployment != null)
                    {
                        var assignRequest = new AssignDeploymentsRequest();
                        Console.WriteLine("Found a deployment, assigning it to the party.");
                        assignRequest.Assignments.Add(new Assignment
                        {
                            DeploymentId = deployment.Id.ToString(),
                            DeploymentName = deployment.DeploymentName,
                            Result = Assignment.Types.Result.Matched,
                            Party = party.Party
                        });
                        gatewayClient.AssignDeployments(assignRequest);
                    }
                    else
                    {
                        Console.WriteLine(
                            $"Unable to find a deployment with tag {_tag} in project {_project}");
                        Console.WriteLine("Requeueing the party");
                        AssignPartyAsRequeued(gatewayClient, party);
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

        protected override void DoShutdown(GatewayInternalService.GatewayInternalServiceClient gatewayClient,
            DeploymentServiceClient deploymentServiceClient,
            DeploymentMetadataService.DeploymentMetadataServiceClient metadataClient)
        {
            // If a matcher maintains state, here is where you hand it back to the Gateway if necessary.
        }

        private void AssignPartyAsRequeued(GatewayInternalService.GatewayInternalServiceClient gatewayClient,
            WaitingParty party)
        {
            var assignRequest = new AssignDeploymentsRequest();
            assignRequest.Assignments.Add(new Assignment
            {
                Result = Assignment.Types.Result.Requeued,
                Party = party.Party
            });
            gatewayClient.AssignDeployments(assignRequest);
        }

        private Deployment GetReadyDeploymentWithTag(
            DeploymentServiceClient deploymentServiceClient,
            string tag)
        {
            return deploymentServiceClient
                .ListDeployments(new ListDeploymentsRequest
                {
                    ProjectName = _project,
                    Filters =
                    {
                        new Filter
                        {
                            TagsPropertyFilter = new TagsPropertyFilter
                            {
                                Operator = TagsPropertyFilter.Types.Operator.Equal,
                                Tag = tag
                            },
                            StoppedStatusPropertyFilter = new StoppedStatusPropertyFilter
                            {
                                StoppedStatus = StoppedStatusPropertyFilter.Types.StoppedStatus.NotStoppedDeployments
                            }
                        }
                    }
                }).FirstOrDefault(d => d.Status == Deployment.Types.Status.Running);
        }
    }
}
