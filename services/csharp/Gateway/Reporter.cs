using Improbable.OnlineServices.DataModel.Gateway;
using Prometheus;

namespace Gateway
{
    public static class Reporter
    {
        private static Counter _joinCounter;
        private static Counter _operationStateCounter;
        private static Counter _cancelOperationCounter;
        private static Counter _transactionAbortedCounter;
        private static Histogram _spatialClientHistogram;

        static Reporter()
        {
            _joinCounter = Metrics.CreateCounter("i8e_gateway_join_request_total", "Total number of join requests.",
                "state");
            _operationStateCounter = Metrics.CreateCounter("i8e_gateway_operation_state_request_total",
                "Total number of operational state requests.", "state");
            _cancelOperationCounter = Metrics.CreateCounter("i8e_gateway_cancel_operation_request_total",
                "Total number of cancel operations.", "state");
            _transactionAbortedCounter = Metrics.CreateCounter("i8e_gateway_transaction_aborted_total",
                "Total number of transactions aborted", "RPC");
            _spatialClientHistogram = Metrics.CreateHistogram("i8e_gateway_spatial_calls_seconds_total",
                "Histogram for requests for waiting players.",
                buckets: new[] { .001, .005, .01, .05, 0.075, .1, .25, .5, 1, 2, 5, 10 },
                labelNames: "method");
        }

        public static void JoinRequestInc()
        {
            _joinCounter.Labels("New").Inc();
        }

        public static void JoinRequestQueuedInc()
        {
            _joinCounter.Labels("Queued").Inc();
        }

        public static void OperationStateInc(MatchState state)
        {
            _operationStateCounter.Labels(state.ToString("G")).Inc();
        }

        public static void OperationStateNotFoundInc()
        {
            _operationStateCounter.Labels("NotFound").Inc();
        }

        public static void CancelOperationInc()
        {
            _cancelOperationCounter.Labels("Success").Inc();
        }

        public static void CancelOperationNotFoundInc()
        {
            _cancelOperationCounter.Labels("NotFound").Inc();
        }

        public static void SpatialCallsInc(string method, double sec)
        {
            _spatialClientHistogram.Labels(method).Observe(sec);
        }

        public static void TransactionAbortedInc(string rpc)
        {
            _transactionAbortedCounter.Labels(rpc).Inc();
        }
    }
}
