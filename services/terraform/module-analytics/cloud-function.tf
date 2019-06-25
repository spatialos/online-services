# This file stores our .zip file in GCS & subsequently deploys the Cloud Function.

# Store the .zip file in GCS.
resource "google_storage_bucket_object" "function_analytics" {
  name   = "analytics/function-gcs-to-bq-${random_pet.cloud_function_pet.id}.zip"
  bucket = "${google_storage_bucket.functions_bucket.name}"
  source = "${path.module}/../../python/analytics-pipeline/cloud-function-analytics.zip"
}

# We attach a random pet name to the name of our cloud function to force a refresh
# whenever the source code changes.
resource "random_pet" "cloud_function_pet" {
  length = 1
  keepers = {
    file_hash = "${data.archive_file.cloud_function_analytics.output_md5}"
  }
}

# Deploy the Cloud Function.
resource "google_cloudfunctions_function" "function_analytics" {
  name                  = "function-gcs-to-bq-${random_pet.cloud_function_pet.id}"
  description           = "GCS to BigQuery Cloud Function"
  runtime               = "python37"

  available_memory_mb   = 256
  source_archive_bucket = "${google_storage_bucket.functions_bucket.name}"
  source_archive_object = "${google_storage_bucket_object.function_analytics.name}"
  timeout               = 60
  entry_point           = "cf0GcsToBq"
  service_account_email = "${google_service_account.cloud_function_gcs_to_bq.email}"

  event_trigger {
    event_type = "google.pubsub.topic.publish"
    resource   = "${google_pubsub_topic.cloud_function_gcs_to_bq_topic.name}"
  }
}
