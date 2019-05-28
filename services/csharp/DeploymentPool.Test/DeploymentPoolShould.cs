using System.Collections.Generic;
using System.Linq;
using System.Net;
using CSharpx;
using Improbable.SpatialOS.Deployment.V1Alpha1;
using Improbable.SpatialOS.Snapshot.V1Alpha1;
using Moq;
using NUnit.Framework;

namespace DeploymentPool.Test
{
    [TestFixture]
    public class DeploymentPoolShouldTest
    {
        private const int MinimumReady = 3;

        private Mock<DeploymentServiceClient> _deploymentSvcMock;
        private Mock<SnapshotServiceClient> _snapshotSvcMock;
        private Mock<IWebRequestCreate> _webRequestMock;
        private Mock<HttpWebRequest> _httpRequestMock;
        private DeploymentPoolManager dplPoolManager;

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
            };
            dplPoolManager = new DeploymentPoolManager(
                args,
                null,
                null
            );
        }

        [Test]
        public void StartsAllDeploymentsIfNoneAreFound()
        {
            var deploymentList = new List<Deployment>();

            var actions = dplPoolManager.GetRequiredActions(deploymentList);

            Assert.AreEqual(3, actions.Count());
            Assert.True(actions.All(dpl => dpl.GetActionType() == DeploymentAction.ActionType.CREATE));
        }

        [Test]
        public void StartsSomeDeploymentsIfPartiallyReady()
        {
            var deploymentList = new List<Deployment>();
            deploymentList.Add(createReadyDeployment());
            deploymentList.Add(createStartingDeployment());

            var actions = dplPoolManager.GetRequiredActions(deploymentList);

            Assert.AreEqual(1, actions.Count());
            Assert.True(actions.All(dpl => dpl.GetActionType() == DeploymentAction.ActionType.CREATE));
        }

        [Test]
        public void TransitionsDeploymentsToReadyOnceStarted()
        {
            var startedDeployment = createStartingDeployment();
            startedDeployment.Status = Deployment.Types.Status.Running;

            var deploymentList = new List<Deployment>();
            deploymentList.Add(createReadyDeployment());
            deploymentList.Add(createReadyDeployment());
            deploymentList.Add(startedDeployment);

            var actions = dplPoolManager.GetRequiredActions(deploymentList);

            Assert.AreEqual(1, actions.Count());
            Assert.True(actions.All(dpl => dpl.GetActionType() == DeploymentAction.ActionType.UPDATE));

            var action = actions.First();
            Assert.AreSame(startedDeployment, action.GetDeployment());
            Assert.AreEqual(1, startedDeployment.Tag.Count);
            Assert.Contains("ready", startedDeployment.Tag);
        }

        [Test]
        public void StopsCompletedDeployments()
        {
            var deploymentList = new List<Deployment>();
            deploymentList.Add(createReadyDeployment());
            deploymentList.Add(createReadyDeployment());
            deploymentList.Add(createReadyDeployment());
            deploymentList.Add(createCompleteDeployment());
            deploymentList.Add(createCompleteDeployment());
            deploymentList.Add(createCompleteDeployment());

            var actions = dplPoolManager.GetRequiredActions(deploymentList);

            Assert.AreEqual(3, actions.Count());
            Assert.True(actions.All(dpl => dpl.GetActionType() == DeploymentAction.ActionType.STOP));
        }

        private Deployment createReadyDeployment()
        {
            var dpl = new Deployment();
            dpl.Name = "readyDeployment";
            dpl.Tag.Add("ready");
            return dpl;
        }

        private Deployment createStartingDeployment()
        {
            var dpl = new Deployment();
            dpl.Name = "startingDeployment";
            dpl.Status = Deployment.Types.Status.Starting;
            dpl.Tag.Add("starting");
            return dpl;
        }

        private Deployment createCompleteDeployment()
        {
            var dpl = new Deployment();
            dpl.Name = "completedDeployment";
            dpl.Tag.Add("completed");
            return dpl;
        }

    }
}
