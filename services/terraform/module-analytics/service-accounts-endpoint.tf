# This file creates the Service Accounts we use for our Cloud Endpoint. It also
# creates their keys & mounts them into our Kubernetes cluster.

# Create analytics-gcs-writer Service Account.
resource "google_service_account" "analytics_gcs_writer_sa" {
  account_id   = "analytics-gcs-writer-${var.environment}"
  display_name = "Analytics GCS Writer ${var.environment}"
}

# Grant the Service Account write rights to our specific GCS bucket.
variable "bucket_write_roles" {
  type        = list(string)
  default     = ["roles/storage.legacyBucketReader", "roles/storage.objectCreator"]
}

resource "google_storage_bucket_iam_member" "analytics_gcs_writer_binding" {

  # Ensures the analytics_bucket is created before this operation is attempted.
  depends_on = [
    google_storage_bucket.analytics_bucket
  ]
  count  = length(var.bucket_write_roles)

  bucket = "${var.gcloud_project}-analytics-${var.environment}"
  role   = var.bucket_write_roles[count.index]
  member = "serviceAccount:${google_service_account.analytics_gcs_writer_sa.email}"
}

# Create a JSON key file for the Service Account.
resource "google_service_account_key" "analytics_gcs_writer_key_json" {
  service_account_id = google_service_account.analytics_gcs_writer_sa.name
  private_key_type   = "TYPE_GOOGLE_CREDENTIALS_FILE" # {TYPE_PKCS12_FILE, TYPE_GOOGLE_CREDENTIALS_FILE}
}

# Create a P12 key file for the Service Account.
resource "google_service_account_key" "analytics_gcs_writer_key_p12" {
  service_account_id = google_service_account.analytics_gcs_writer_sa.name
  private_key_type   = "TYPE_PKCS12_FILE" # {TYPE_PKCS12_FILE, TYPE_GOOGLE_CREDENTIALS_FILE}
}

# Create a Kubernetes JSON secret.
resource "kubernetes_secret" "analytics_gcs_writer_key_json_k8s" {
  metadata {
    name = "analytics-gcs-writer-json-${var.environment}"
  }
  data = {
    "analytics-gcs-writer.json" = base64decode(google_service_account_key.analytics_gcs_writer_key_json.private_key)
  }
}

# Create a Kubernetes P12 secret.
resource "kubernetes_secret" "analytics_gcs_writer_key_p12_k8s" {
  metadata {
    name = "analytics-gcs-writer-p12-${var.environment}"
  }
  data = {
    "analytics-gcs-writer.p12" = google_service_account_key.analytics_gcs_writer_key_p12.private_key
  }
}

# Create endpoints-credentials Service Account.
resource "google_service_account" "analytics_endpoint_sa" {
  account_id   = "analytics-endpoint-${var.environment}"
  display_name = "Analytics Endpoint"
}

# Add the roles/cloudtrace.agent role.
resource "google_project_iam_member" "cloudtrace_agent_role" {
  role   = "roles/cloudtrace.agent"
  member = "serviceAccount:${google_service_account.analytics_endpoint_sa.email}"
}

# Add the roles/servicemanagement.serviceController role.
resource "google_project_iam_member" "service_management_controller_role" {
  role   = "roles/servicemanagement.serviceController"
  member = "serviceAccount:${google_service_account.analytics_endpoint_sa.email}"
}

# Create a JSON key file.
resource "google_service_account_key" "analytics_endpoint_key_json" {
  service_account_id = google_service_account.analytics_endpoint_sa.name
}

# Create a Kubernetes JSON secret.
resource "kubernetes_secret" "analytics_endpoint_key_json_k8s" {
  metadata {
    name = "analytics-endpoint-json-${var.environment}"
  }
  data = {
    "analytics-endpoint.json" = base64decode(google_service_account_key.analytics_endpoint_key_json.private_key)
  }
}
