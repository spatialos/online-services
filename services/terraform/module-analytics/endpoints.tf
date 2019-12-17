# This file enables a required API & creates our Cloud Endpoint service.

# Create analytics endpoint.
resource "google_endpoints_service" "analytics_endpoint" {
  service_name   = "analytics-${var.environment}.endpoints.${var.gcloud_project}.cloud.goog"
  project        = var.gcloud_project
  openapi_config = templatefile("./module-analytics/spec/analytics-endpoint.yml", { project: var.gcloud_project, target: google_compute_address.analytics_ip.address, environment: var.environment })
}

# Enable analytics endpoint.
resource "google_project_service" "service_analytics" {

  depends_on = [
    google_endpoints_service.analytics_endpoint
  ]

  project            = var.gcloud_project
  service            = "analytics-${var.environment}.endpoints.${var.gcloud_project}.cloud.goog"
  disable_on_destroy = true
}

# Note - if you recently applied & tore down your endpoint, and you are trying to re-apply the endpoint within 30 days, you might get the following error:

# Error: googleapi: Error 400: Service analytics.endpoints.{GCLOUD_PROJECT_ID}.cloud.goog has been deleted and will be purged after 30 days.
# To reuse this service, please undelete the service following https://cloud.google.com/service-management/create-delete., failedPrecondition

# If this is the case, first undelete the endpoint before re-applying it with Terraform, by running:
# `gcloud endpoints services undelete analytics.endpoints.{GCLOUD_PROJECT_ID}.cloud.goog`

# Declare output variable.
output "analytics_dns" {
  value = google_endpoints_service.analytics_endpoint.dns_address
}
