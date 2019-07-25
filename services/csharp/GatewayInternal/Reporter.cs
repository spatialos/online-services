using Improbable.OnlineServices.Proto.Gateway;
using Prometheus;

namespace GatewayInternal
{
    public static class Reporter
    {
        private static Counter _assignCounter;
        private static Counter _transactionAbortedCounter;
        private static Histogram _waitingHistogram;
        private static Histogram _waitingInsufficientHistogram;

        static Reporter()
        {
            _assignCounter = Metrics.CreateCounter("i8e_gatewayinternal_assign_request_total", "Total number of assign deployment requests.", "deployment", "result");
            _transactionAbortedCounter = Metrics.CreateCounter("i8e_gateway_transaction_aborted_total",
                "Total number of transactions aborted", "RPC");
            _waitingHistogram = Metrics.CreateHistogram("i8e_gatewayinternal_waiting_parties_request_total",
                "Histogram for requests for waiting parties.",
                buckets: new[] { 0, 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, double.MaxValue });
            _waitingInsufficientHistogram = Metrics.CreateHistogram("i8e_gatewayinternal_waiting_parties_insufficient_request_total",
                "Histogram for requests for waiting parties.",
                buckets: new[] { 0, 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, double.MaxValue });
        }

        public static void AssignDeploymentInc(string deploymentId, Assignment.Types.Result result)
        {
            _assignCounter.Labels(deploymentId, result.ToString("G")).Inc();
        }

        public static void AssignDeploymentNotFoundInc(string deploymentId)
        {
            _assignCounter.Labels(deploymentId, "NotFound").Inc();
        }

        public static void GetWaitingPartiesInc(uint requestNumParties)
        {
            _waitingHistogram.Observe(requestNumParties);
        }

        public static void InsufficientWaitingPartiesInc(uint requestNumParties)
        {
            _waitingInsufficientHistogram.Observe(requestNumParties);
        }

        public static void TransactionAbortedInc(string rpc)
        {
            _transactionAbortedCounter.Labels(rpc).Inc();
        }
    }
}
