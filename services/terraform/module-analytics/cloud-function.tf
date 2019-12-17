# This file stores our .zip file in GCS & subsequently deploys the Cloud Function.

# Store the .zip files in GCS.
resource "google_storage_bucket_object" "function_improbable_schema" {
  name   = "analytics/function-improbable-schema-${var.environment}-${random_pet.function_improbable_schema.id}.zip"
  bucket = google_storage_bucket.functions_bucket.name
  source = "${path.module}/../../python/analytics-pipeline/cloud-function-improbable-schema-${var.environment}.zip"
}

resource "google_storage_bucket_object" "function_playfab_schema" {
  name   = "analytics/function-playfab-schema-${var.environment}-${random_pet.function_playfab_schema.id}.zip"
  bucket = google_storage_bucket.functions_bucket.name
  source = "${path.module}/../../python/analytics-pipeline/cloud-function-playfab-schema-${var.environment}.zip"
}

# We attach a random pet name to the name of our cloud function to force a refresh
# whenever the source code changes.
resource "random_pet" "function_improbable_schema" {
  length  = 1
  keepers = {
    file_hash = data.archive_file.cloud_function_improbable_schema.output_md5
  }
}

resource "random_pet" "function_playfab_schema" {
  length  = 1
  keepers = {
    file_hash = data.archive_file.cloud_function_playfab_schema.output_md5
  }
}

# Deploy the Cloud Functions.
resource "google_cloudfunctions_function" "function_improbable_schema" {
  name                  = "function-improbable-schema-${var.environment}-${random_pet.function_improbable_schema.id}"
  description           = "GCS to Native BigQuery Cloud Function"
  runtime               = "python37"

  available_memory_mb   = 128
  source_archive_bucket = google_storage_bucket.functions_bucket.name
  source_archive_object = google_storage_bucket_object.function_improbable_schema.name
  timeout               = 180
  # The name of the Python function to invoke in ../../python/function/main.py:
  entry_point           = "ingest_into_native_bigquery_storage"
  service_account_email = google_service_account.cloud_function_gcs_to_bq.email

  event_trigger {
    event_type = "google.pubsub.topic.publish"
    resource   = google_pubsub_topic.cloud_function_improbable_schema.name
  }

  environment_variables = {
    LOCATION    = var.cloud_storage_location
    ENVIRONMENT = var.environment
  }
}

resource "google_cloudfunctions_function" "function_playfab_schema" {
  name                  = "function-playfab-schema-${var.environment}-${random_pet.function_improbable_schema.id}"
  description           = "GCS to Native BigQuery Cloud Function"
  runtime               = "python37"

  available_memory_mb   = 128
  source_archive_bucket = google_storage_bucket.functions_bucket.name
  source_archive_object = google_storage_bucket_object.function_playfab_schema.name
  timeout               = 180
  # The name of the Python function to invoke in ../../python/function/main.py:
  entry_point           = "ingest_into_native_bigquery_storage"
  service_account_email = google_service_account.cloud_function_gcs_to_bq.email

  event_trigger {
    event_type = "google.pubsub.topic.publish"
    resource   = google_pubsub_topic.cloud_function_playfab_schema.name
  }

  environment_variables = {
    LOCATION    = var.cloud_storage_location
    ENVIRONMENT = var.environment
  }
}
