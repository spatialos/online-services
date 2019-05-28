using System.ComponentModel;
using Prometheus;

namespace DeploymentPool
{
    public class Reporter
    {
        private static readonly double[] timingBuckets =
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
        public static readonly Counter DeploymentCreationCount = Metrics.CreateCounter("deployment_creation_requested_total", "Total Deployment Creations requested", labels);
        public static readonly Counter DeploymentCreationFailures = Metrics.CreateCounter("deployment_creation_failures_total", "Total Deployment Creation Exceptions encountered", labels);
        public static readonly Counter DeploymentStopCount = Metrics.CreateCounter("deployment_stop_requested_total", "Total Deployment Stops requested.", labels);
        public static readonly Counter DeploymentStopFailures = Metrics.CreateCounter("deployment_stop_failures_total", "Total Deployment Stop Exceptions encountered", labels);
        public static readonly Counter DeploymentUpdateCount = Metrics.CreateCounter("deployment_update_requested_total", "Total Deployment Updates requested.", labels);
        public static readonly Counter DeploymentUpdateFailures = Metrics.CreateCounter("deployment_update_failures_total", "Total Deployment Updates Exceptions encountered.", labels);
        public static readonly Histogram DeploymentCreationTime = Metrics.CreateHistogram(
            "deployment_creation_time_seconds", "Time to start a deployment.",
            new HistogramConfiguration { Buckets = timingBuckets, LabelNames = labels }
        );
        public static readonly Histogram DeploymentStopTime = Metrics.CreateHistogram(
            "deployment_stop_time_seconds", "Time to stop a deployment.",
            new HistogramConfiguration { Buckets = timingBuckets, LabelNames = labels }
        );

        public static readonly Gauge DeploymentsInReadyState = Metrics.CreateGauge("deployment_ready_state",
            "Current number of deployments in the ready state", labels);

        public static readonly Gauge DeploymentsInStartingState = Metrics.CreateGauge("deployment_starting_state",
            "Current number of deployments in the starting state", labels);
    }
}