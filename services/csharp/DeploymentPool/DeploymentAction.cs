using Improbable.SpatialOS.Deployment.V1Alpha1;

namespace DeploymentPool
{
    public class DeploymentAction
    {
        public Deployment deployment { get; }
        public ActionType actionType { get; }

        public enum ActionType
        {
            Create,
            Update,
            Stop,
        }

        private DeploymentAction(ActionType actionType,
            Deployment deployment = null)
        {
            this.actionType = actionType;
            this.deployment = deployment;
        }

        public static DeploymentAction NewCreationAction()
        {
            return new DeploymentAction(ActionType.Create);
        }

        public static DeploymentAction NewUpdateAction(Deployment deployment)
        {
            return new DeploymentAction(ActionType.Update, deployment);
        }

        public static DeploymentAction NewStopAction(Deployment deployment)
        {
            return new DeploymentAction(ActionType.Stop, deployment);
        }
    }
}
