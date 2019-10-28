# This file creates the Service Account we use for our Cloud Function.

# Provision Service Account.
resource "google_service_account" "cloud_function_gcs_to_bq" {
  account_id   = "function-gcs-to-bq"
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

# Add the roles/storage.admin role.
resource "google_project_iam_member" "storage_role" {
  role   = "roles/storage.admin"
  member = "serviceAccount:${google_service_account.cloud_function_gcs_to_bq.email}"
}

# Add the roles/cloudfunctions.developer role.
resource "google_project_iam_member" "cf_dev_role" {
  role   = "roles/cloudfunctions.developer"
  member = "serviceAccount:${google_service_account.cloud_function_gcs_to_bq.email}"
}
