using System;
using System.Linq;
using System.Threading;
using Grpc.Core;
using Improbable.OnlineServices.Proto.Gateway;
using Improbable.OnlineServices.Proto.Metadata;
using Improbable.SpatialOS.Deployment.V1Beta1;

namespace Improbable.OnlineServices.SampleMatcher
{
    public class PoolMatcher : Base.Matcher.Matcher
    {
        private const int TickMs = 200;
        private const string DefaultMatchTag = "match";
        private readonly string _project;
        private readonly string _tag;
        private readonly string ReadinessKey = "readiness";
        private readonly string ReadyTag = "ready"; // This should be the same tag a DeploymentPool looks for.
        private readonly string InUseTag = "in_use";

        public PoolMatcher()
        {
            _project = Environment.GetEnvironmentVariable("SPATIAL_PROJECT");
            _tag = Environment.GetEnvironmentVariable("MATCH_TAG") ?? DefaultMatchTag;
        }

        protected override void DoMatch(GatewayInternalService.GatewayInternalServiceClient gatewayClient,
            DeploymentServiceClient deploymentServiceClient,
            DeploymentMetadataService.DeploymentMetadataServiceClient metadataClient)
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
                    var deployment = GetReadyDeploymentWithTag(deploymentServiceClient, metadataClient, _tag);
                    if (deployment != null && MarkDeploymentAsInUse(deploymentServiceClient, metadataClient, deployment))
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
            DeploymentMetadataService.DeploymentMetadataServiceClient metadataClient,
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
                }).FirstOrDefault(d =>
                {
                    if (d.Status != Deployment.Types.Status.Running)
                    {
                        return false;
                    }

                    return GetDeploymentReadiness(metadataClient, d.Id.ToString()) == ReadyTag;
                });
        }

        private string GetDeploymentReadiness(
            DeploymentMetadataService.DeploymentMetadataServiceClient metadataClient, string deploymentId)
        {
            try
            {
                var resp = metadataClient.GetDeploymentMetadata(new GetDeploymentMetadataRequest
                {
                    DeploymentId = deploymentId
                });
                return resp.Value[ReadinessKey];
            }
            catch (RpcException e)
            {
                Console.WriteLine($"Failed to get readiness for deployment {deploymentId}. Error: {e}");
                return null;
            }
        }

        private bool MarkDeploymentAsInUse(DeploymentServiceClient dplClient,
            DeploymentMetadataService.DeploymentMetadataServiceClient metadataClient, Deployment dpl)
        {
            try
            {
                // Transition from Ready to InUse
                var setDeploymentMetadataRequest = new SetDeploymentMetadataEntryRequest
                {
                    Condition = new Condition
                    {
                        Function = Condition.Types.Function.Equal,
                        Payload = ReadyTag
                    },
                    DeploymentId = dpl.Id.ToString(),
                    Key = ReadinessKey,
                    Value = InUseTag
                };
                metadataClient.SetDeploymentMetadataEntry(setDeploymentMetadataRequest);

                // Also set the tag on the deployment, so it's visible in the console.
                dpl.Tags.Remove(ReadyTag);
                dpl.Tags.Add(InUseTag);
                var req = new SetDeploymentTagsRequest
                {
                    DeploymentId = dpl.Id
                };
                req.Tags.AddRange(dpl.Tags);
                dplClient.SetDeploymentTags(req);

                return true;
            }
            catch (RpcException e)
            {
                if (e.StatusCode == StatusCode.FailedPrecondition || e.StatusCode == StatusCode.InvalidArgument)
                {
                    Console.WriteLine($"Metadata and tags for deployment {dpl} couldn't be updated. Error {e}", dpl.DeploymentName, e.Message);
                    return false;
                }
            }

            return false;
        }
    }
}
