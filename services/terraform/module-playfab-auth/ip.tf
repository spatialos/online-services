# This file defines the external IP addresses needed to expose the services.

resource "google_compute_address" "playfab_auth_ip" {
    name   = "${var.k8s_cluster_name}-playfab-auth-address-${var.environment}"
    region = var.gcloud_region
}

output "playfab_auth_host" {
  value = google_compute_address.playfab_auth_ip.address
}
