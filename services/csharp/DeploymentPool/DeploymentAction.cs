using Improbable.SpatialOS.Deployment.V1Alpha1;

namespace DeploymentPool
{
    public class DeploymentAction
    {
        private Deployment deployment;
        private ActionType actionType;

        public enum ActionType
        {
            CREATE,
            UPDATE,
            STOP,
        }

        public ActionType GetActionType()
        {
            return actionType;
        }

        public Deployment GetDeployment()
        {
            return deployment;
        }

        private DeploymentAction(ActionType actionType,
            Deployment deployment = null)
        {
            this.actionType = actionType;
            this.deployment = deployment;
        }

        public static DeploymentAction NewCreationAction()
        {
            return new DeploymentAction(ActionType.CREATE);
        }

        public static DeploymentAction NewUpdateAction(Deployment deployment)
        {
            return new DeploymentAction(ActionType.UPDATE, deployment);
        }

        public static DeploymentAction NewStopAction(Deployment deployment)
        {
            return new DeploymentAction(ActionType.STOP, deployment);
        }
    }
}