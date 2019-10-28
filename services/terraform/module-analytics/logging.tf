# Disabling INFO logging on the bucket that stores our analytics events,
# as using GCS as a federated data source results in loads of logs.

resource "google_logging_project_exclusion" "exclude_gcs_bucket_info" {
    name        = "exclude-analytics-gcs-bucket-info-logs"
    description = "Exclude all our INFO based logs of the analytics bucket."
    # Exclude all INFO severity messages relating to gcs_buckets
    filter      = "resource.type = gcs_bucket AND resource.labels.bucket_name=\"${var.gcloud_project}-analytics\" AND severity = INFO"
}
