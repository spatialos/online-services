# This file creates the Service Accounts we use for our Dataflow Batch backfill script.
# It also creates a key & mounts it into our Kubernetes cluster.

# Create dataflow_gcs_to_bq_batch Service Account.
resource "google_service_account" "dataflow_batch" {
  account_id   = "dataflow-batch"
  display_name = "Dataflow Batch"
}

# Grant the Service Account Admin rights to our specific GCS bucket.
resource "google_storage_bucket_iam_member" "dataflow_batch_gcs_binding" {
  bucket = "${var.gcloud_project}-analytics"
  role   = "roles/storage.admin"
  member = "serviceAccount:${google_service_account.dataflow_batch.email}"

  # Ensures the analytics_bucket is created before this operation is attempted.
  depends_on = [google_storage_bucket.analytics_bucket]
}

# Add the roles/bigquery.admin role
resource "google_project_iam_member" "bigquery_admin_role_batch" {
  role   = "roles/bigquery.admin"
  member = "serviceAccount:${google_service_account.dataflow_batch.email}"
}

# Add the roles/dataflow.admin role.
resource "google_project_iam_member" "dataflow_admin_role_batch" {
  role   = "roles/dataflow.admin"
  member = "serviceAccount:${google_service_account.dataflow_batch.email}"
}

# Add the roles/dataflow.worker role.
resource "google_project_iam_member" "dataflow_worker_role_batch" {
  role    = "roles/dataflow.worker"
  member  = "serviceAccount:${google_service_account.dataflow_batch.email}"
}

# Add the roles/iam.serviceAccountUser role.
resource "google_project_iam_member" "sa_user_role_batch" {
  role   = "roles/iam.serviceAccountUser"
  member = "serviceAccount:${google_service_account.dataflow_batch.email}"
}

# Add the roles/pubsub.admin role.
resource "google_project_iam_member" "pubsub_admin_role_batch" {
  role   = "roles/pubsub.admin"
  member = "serviceAccount:${google_service_account.dataflow_batch.email}"
}

# Create a key file for the Service Account.
resource "google_service_account_key" "dataflow_batch" {
  service_account_id = "${google_service_account.dataflow_batch.name}"
}

# Create a kubernetes secret called "dataflow-batch".
resource "kubernetes_secret" "dataflow_batch" {
  metadata {
    name = "dataflow-batch"
  }

  data = {
    "dataflow-batch.json" = "${base64decode(google_service_account_key.dataflow_batch.private_key)}"
  }
}
