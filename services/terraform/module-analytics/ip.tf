# This file defines the external IP address needed to expose the analytics service.

# Create the address.
resource "google_compute_address" "analytics_ip" {
  name   = "${var.k8s_cluster_name}-analytics-address"
  region = var.gcloud_region
}

# Declare output variable.
output "analytics_host" {
  value = google_compute_address.analytics_ip.address
}
