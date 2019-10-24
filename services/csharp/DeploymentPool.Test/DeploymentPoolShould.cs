using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Improbable.SpatialOS.Deployment.V1Beta1;
using NUnit.Framework;

namespace DeploymentPool.Test
{
    [TestFixture]
    public class DeploymentPoolShouldTest
    {
        private const int MinimumReady = 3;
        private const string ReadyTag = "ready";
        private const string StartingTag = "starting";
        private const string StoppingTag = "stopping";
        private const string CompletedTag = "completed";

        private DeploymentPool dplPoolManager;

        [SetUp]
        public void Setup()
        {
            var args = new DeploymentPoolArgs
            {
                AssemblyName = "assembly",
                DeploymentNamePrefix = "prefix",
                SpatialProject = "project",
                MatchType = "testing",
                MinimumReadyDeployments = MinimumReady,
                Cleanup = true
            };
            dplPoolManager = new DeploymentPool(
                args,
                null,
                null,
                null,
                new CancellationToken()
            );
        }

        [Test]
        public void StartsAllDeploymentsIfNoneAreFound()
        {
            var deploymentList = new List<(Deployment deployment, string readiness)>();

            var actions = dplPoolManager.GetRequiredActions(deploymentList);

            Assert.AreEqual(3, actions.Count());
            Assert.True(actions.All(dpl => dpl.actionType == DeploymentAction.ActionType.Create));
            Assert.True(actions.All(dpl => dpl.oldReadiness == null));
            Assert.True(actions.All(dpl => dpl.newReadiness == StartingTag));
        }

        [Test]
        public void StartsSomeDeploymentsIfPartiallyReady()
        {
            var deploymentList = new List<(Deployment deployment, string readiness)>();
            deploymentList.Add(CreateReadyDeployment());
            deploymentList.Add(CreateStartingDeployment());

            var actions = dplPoolManager.GetRequiredActions(deploymentList);

            Assert.AreEqual(1, actions.Count());
            Assert.True(actions.All(dpl => dpl.actionType == DeploymentAction.ActionType.Create));
            Assert.True(actions.All(dpl => dpl.oldReadiness == null));
            Assert.True(actions.All(dpl => dpl.newReadiness == StartingTag));
        }

        [Test]
        public void TransitionsDeploymentsToReadyOnceStarted()
        {
            var startedDeployment = CreateStartingDeployment();
            startedDeployment.deployment.Status = Deployment.Types.Status.Running;
            startedDeployment.readiness = StartingTag;

            var deploymentList = new List<(Deployment deployment, string readiness)>();
            deploymentList.Add(CreateReadyDeployment());
            deploymentList.Add(CreateReadyDeployment());
            deploymentList.Add(startedDeployment);

            var actions = dplPoolManager.GetRequiredActions(deploymentList);

            Assert.AreEqual(1, actions.Count());
            var action = actions.First();
            Assert.AreEqual(DeploymentAction.ActionType.Update, action.actionType);
            Assert.AreEqual(1, action.deployment.Tags.Count);
            Assert.Contains(ReadyTag, action.deployment.Tags);
            Assert.AreEqual(StartingTag, action.oldReadiness);
            Assert.AreEqual(ReadyTag, action.newReadiness);
        }

        [Test]
        public void StopsCompletedDeployments()
        {
            var deploymentList = new List<(Deployment deployment, string readiness)>();
            deploymentList.Add(CreateReadyDeployment());
            deploymentList.Add(CreateReadyDeployment());
            deploymentList.Add(CreateReadyDeployment());
            deploymentList.Add(CreateCompleteDeployment());

            var actions = dplPoolManager.GetRequiredActions(deploymentList);

            Assert.AreEqual(1, actions.Count());
            var action = actions.First();
            Assert.AreEqual(DeploymentAction.ActionType.Stop, action.actionType);
            Assert.AreEqual(2, action.deployment.Tags.Count);
            Assert.Contains(StoppingTag, action.deployment.Tags);
            Assert.Contains(CompletedTag, action.deployment.Tags);
            Assert.AreEqual(CompletedTag, action.oldReadiness);
            Assert.AreEqual(StoppingTag, action.newReadiness);
        }

        [Test]
        public void DoesNotModifyDeploymentsThatAreAlreadyStopping()
        {
            var deploymentList = new List<(Deployment deployment, string readiness)>();
            deploymentList.Add(CreateReadyDeployment());
            deploymentList.Add(CreateReadyDeployment());
            deploymentList.Add(CreateReadyDeployment());
            deploymentList.Add(CreateStoppingDeployment());

            var actions = dplPoolManager.GetRequiredActions(deploymentList);

            Assert.AreEqual(0, actions.Count());
        }

        private (Deployment deployment, string readiness) CreateReadyDeployment()
        {
            var dpl = new Deployment();
            dpl.DeploymentName = "readyDeployment";
            dpl.Tags.Add(ReadyTag);
            return (dpl, ReadyTag);
        }

        private (Deployment deployment, string readiness) CreateStartingDeployment()
        {
            var dpl = new Deployment();
            dpl.DeploymentName = "startingDeployment";
            dpl.Status = Deployment.Types.Status.Starting;
            dpl.Tags.Add(StartingTag);
            return (dpl, StartingTag);
        }

        private (Deployment deployment, string readiness) CreateCompleteDeployment()
        {
            var dpl = new Deployment();
            dpl.DeploymentName = "completedDeployment";
            dpl.Tags.Add(CompletedTag);
            return (dpl, CompletedTag);
        }

        private (Deployment deployment, string readiness) CreateStoppingDeployment()
        {
            var dpl = new Deployment();
            dpl.DeploymentName = "stoppingDeployment";
            // Stopping deployments have both completed and stopping tags in the current implementation
            dpl.Tags.Add(StoppingTag);
            dpl.Tags.Add(CompletedTag);
            return (dpl, StoppingTag);
        }

    }
}
