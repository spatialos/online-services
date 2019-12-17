# This file creates the Service Account we use for our Cloud Function.

# Provision Service Account.
resource "google_service_account" "cloud_function_gcs_to_bq" {
  account_id   = "function-gcs-to-bq-${var.environment}"
  display_name = "Cloud Function GCS to BigQuery"
}

# Add the roles/iam.serviceAccountUser role.
resource "google_project_iam_member" "sa_user_role" {
  role   = "roles/iam.serviceAccountUser"
  member = "serviceAccount:${google_service_account.cloud_function_gcs_to_bq.email}"
}

# Add the roles/pubsub.admin role.
resource "google_project_iam_member" "pubsub_role" {
  role   = "roles/pubsub.admin"
  member = "serviceAccount:${google_service_account.cloud_function_gcs_to_bq.email}"
}

# Add the roles/bigquery.admin role.
resource "google_project_iam_member" "bq_role" {
  role   = "roles/bigquery.admin"
  member = "serviceAccount:${google_service_account.cloud_function_gcs_to_bq.email}"
}

# Grant the Service Account read rights to our specific GCS bucket.
variable "bucket_read_roles" {
  type    = list(string)
  default = ["roles/storage.legacyBucketReader", "roles/storage.objectViewer"]
}

resource "google_storage_bucket_iam_member" "analytics_function_to_bq_binding" {

  # Ensures the analytics_bucket is created before this operation is attempted.
  depends_on = [
    google_storage_bucket.analytics_bucket
  ]
  count  = length(var.bucket_read_roles)

  bucket = "${var.gcloud_project}-analytics-${var.environment}"
  role   = var.bucket_read_roles[count.index]
  member = "serviceAccount:${google_service_account.cloud_function_gcs_to_bq.email}"
}

# Add the roles/cloudfunctions.developer role.
resource "google_project_iam_member" "cf_dev_role" {
  role   = "roles/cloudfunctions.developer"
  member = "serviceAccount:${google_service_account.cloud_function_gcs_to_bq.email}"
}
