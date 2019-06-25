# This file defines the external IP addresses needed to expose the services.

resource "google_compute_address" "gateway_ip" {
    name = "${var.k8s_cluster_name}-gateway-address"
    region = "${var.gcloud_region}"
}

resource "google_compute_address" "party_ip" {
    name = "${var.k8s_cluster_name}-party-address"
    region = "${var.gcloud_region}"
}

resource "google_compute_address" "playfab_auth_ip" {
    name = "${var.k8s_cluster_name}-playfab-auth-address"
    region = "${var.gcloud_region}"
}

output "gateway_host" {
  value = "${google_compute_address.gateway_ip.address}"
}

output "party_host" {
  value = "${google_compute_address.party_ip.address}"
}

output "playfab_auth_host" {
  value = "${google_compute_address.playfab_auth_ip.address}"
}
