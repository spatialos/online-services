using Improbable.SpatialOS.Deployment.V1Alpha1;

namespace DeploymentPool
{
    public class DeploymentAction
    {
        public Deployment deployment { get; }
        public ActionType actionType { get; }
        public string oldReadiness { get; }
        public string newReadiness { get; set; }

        public enum ActionType
        {
            Create,
            Update,
            Stop,
        }

        private DeploymentAction(ActionType actionType,
            Deployment deployment = null,
            string newReadiness = null,
            string oldReadiness = null)
        {
            this.actionType = actionType;
            this.deployment = deployment;
            this.newReadiness = newReadiness;
            this.oldReadiness = oldReadiness;
        }

        public static DeploymentAction NewCreationAction(string newReadiness = null)
        {
            return new DeploymentAction(ActionType.Create, null, newReadiness);
        }

        public static DeploymentAction NewUpdateAction(Deployment deployment, string newReadiness = null, string oldReadiness = null)
        {
            return new DeploymentAction(ActionType.Update, deployment, newReadiness, oldReadiness);
        }

        public static DeploymentAction NewStopAction(Deployment deployment, string newReadiness = null, string oldReadiness = null)
        {
            return new DeploymentAction(ActionType.Stop, deployment, newReadiness, oldReadiness);
        }
    }
}
