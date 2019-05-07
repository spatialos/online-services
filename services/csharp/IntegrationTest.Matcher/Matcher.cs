using System;
using System.Collections.Generic;
using System.Threading;
using Improbable.OnlineServices.Proto.Gateway;
using Improbable.SpatialOS.Deployment.V1Alpha1;

namespace IntegrationTest.Matcher
{
    public class Matcher : Improbable.OnlineServices.Base.Matcher.Matcher
    {
        private const int TickMs = 1000;
        private readonly HashSet<string> _requeued;

        public Matcher()
        {
            _requeued = new HashSet<string>();
        }

        protected override void DoMatch(GatewayInternalService.GatewayInternalServiceClient gatewayClient,
            DeploymentServiceClient deploymentServiceClient)
        {
            Thread.Sleep(TickMs);

            try
            {
                Console.WriteLine("Fetching 1...");
                var resp = gatewayClient.PopWaitingParties(new PopWaitingPartiesRequest
                {
                    Type = "match1",
                    NumParties = 1
                });
                Console.WriteLine($"Fetched {resp.Parties.Count} parties");
                gatewayClient.AssignDeployments(ConstructAssignRequest(resp.Parties, "1"));
            }
            catch (Exception e)
            {
                Console.WriteLine($"Got exception: {e.Message}\n{e.StackTrace}");
            }

            try
            {
                Console.WriteLine("Fetching 3...");
                var resp = gatewayClient.PopWaitingParties(new PopWaitingPartiesRequest
                {
                    Type = "match3",
                    NumParties = 3
                });
                Console.WriteLine($"Fetched {resp.Parties.Count} parties");
                gatewayClient.AssignDeployments(ConstructAssignRequest(resp.Parties, "3"));
            }
            catch (Exception e)
            {
                Console.WriteLine($"Got exception: {e.Message}\n{e.StackTrace}");
            }

            try
            {
                Console.WriteLine("Fetching requeueable...");
                var resp = gatewayClient.PopWaitingParties(new PopWaitingPartiesRequest
                {
                    Type = "to_requeue",
                    NumParties = 1
                });
                Console.WriteLine($"Fetched {resp.Parties.Count} parties");
                gatewayClient.AssignDeployments(ConstructAssignRequest(resp.Parties, "requeue"));
            }
            catch (Exception e)
            {
                Console.WriteLine($"Got exception: {e.Message}\n{e.StackTrace}");
            }
        }

        private AssignDeploymentsRequest ConstructAssignRequest(IEnumerable<WaitingParty> waitingParties,
            string deployment)
        {
            var req = new AssignDeploymentsRequest();
            foreach (var waitingParty in waitingParties)
            {
                var assignment = new Assignment
                {
                    DeploymentId = deployment,
                    DeploymentName = $"test_deployment_{deployment}",
                    Result = Assignment.Types.Result.Matched,
                    Party = waitingParty.Party
                };
                if (deployment == "requeue" && !_requeued.Contains(waitingParty.Party.Id))
                {
                    assignment.Result = Assignment.Types.Result.Requeued;
                    _requeued.Add(waitingParty.Party.Id);
                }
                req.Assignments.Add(assignment);
            }

            return req;
        }

        protected override void DoShutdown(GatewayInternalService.GatewayInternalServiceClient gatewayClient,
            DeploymentServiceClient deploymentServiceClient)
        {
        }
    }
}
