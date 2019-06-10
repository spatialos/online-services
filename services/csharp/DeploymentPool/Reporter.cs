using System.ComponentModel;
using System.IO;
using Prometheus;

namespace DeploymentPool
{
    public class Reporter
    {
        private static readonly double[] timingBucketSeconds =
        {
            // Buckets up to 20 minutes (max start time)
            10.0,
            30.0,
            60.0,
            90.0,
            120.0,
            180.0,
            300.0,
            600.0,
            1200.0
        };

        private static readonly string[] labels = { "matchType" };

        private static readonly Counter DeploymentCreationRequestCount =
            Metrics.CreateCounter("deployment_creation_requests_total", "Total Deployment Creation requests", labels);

        private static readonly Counter DeploymentCreationFailureCount =
            Metrics.CreateCounter("deployment_creation_failures_total",
                "Total Deployment Creation exceptions encountered", labels);

        private static readonly Counter DeploymentStopRequestCount =
            Metrics.CreateCounter("deployment_stop_requests_total", "Total Deployment Stop requests.", labels);

        private static readonly Counter DeploymentStopFailureCount =
            Metrics.CreateCounter("deployment_stop_failures_total", "Total Deployment Stop exceptions encountered",
                labels);

        private static readonly Counter DeploymentUpdateRequestCount =
            Metrics.CreateCounter("deployment_update_requests_total", "Total Deployment Update requests", labels);

        private static readonly Counter DeploymentUpdateFailureCount =
            Metrics.CreateCounter("deployment_update_failures_total", "Total Deployment Update exceptions encountered",
                labels);

        private static readonly Histogram DeploymentCreationDuration = Metrics.CreateHistogram(
            "deployment_creation_time_seconds", "Total time in seconds to start a deployment.",
            new HistogramConfiguration { Buckets = timingBucketSeconds, LabelNames = labels }
        );

        private static readonly Histogram DeploymentStopDuration = Metrics.CreateHistogram(
            "deployment_stop_time_seconds", "Total time in seconds to stop a deployment.",
            new HistogramConfiguration { Buckets = timingBucketSeconds, LabelNames = labels }
        );

        private static readonly Gauge DeploymentsInReadyState = Metrics.CreateGauge("deployment_in_ready_state",
            "Current number of deployments in the ready state", labels);

        private static readonly Gauge DeploymentsInStartingState = Metrics.CreateGauge("deployment_in_starting_state",
            "Current number of deployments in the starting state", labels);

        public static void ReportDeploymentCreationRequest(string matchType)
        {
            DeploymentCreationRequestCount.WithLabels(matchType).Inc();
        }
        public static void ReportDeploymentCreationFailure(string matchType)
        {
            DeploymentCreationFailureCount.WithLabels(matchType).Inc();
        }
        public static void ReportDeploymentStopRequest(string matchType)
        {
            DeploymentStopRequestCount.WithLabels(matchType).Inc();
        }
        public static void ReportDeploymentStopFailure(string matchType)
        {
            DeploymentStopFailureCount.WithLabels(matchType).Inc();
        }
        public static void ReportDeploymentUpdateRequest(string matchType)
        {
            DeploymentUpdateRequestCount.WithLabels(matchType).Inc();
        }
        public static void ReportDeploymentUpdateFailure(string matchType)
        {
            DeploymentUpdateFailureCount.WithLabels(matchType).Inc();
        }
        public static void ReportDeploymentCreationDuration(string matchType, double duration)
        {
            DeploymentCreationDuration.WithLabels(matchType).Observe(duration);
        }
        public static void ReportDeploymentStopDuration(string matchType, double duration)
        {
            DeploymentStopDuration.WithLabels(matchType).Observe(duration);
        }
        public static void ReportDeploymentsInReadyState(string matchType, int number)
        {
            DeploymentsInReadyState.WithLabels(matchType).Set(number);
        }
        public static void ReportDeploymentsInStartingState(string matchType, int number)
        {
            DeploymentsInStartingState.WithLabels(matchType).Set(number);
        }
    }
}