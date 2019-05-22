# This file defines the external IP addresses needed to expose the services

resource "google_compute_global_address" "gateway_ip" {
    name = "gateway-address"
}

resource "google_compute_global_address" "party_ip" {
    name = "party-address"
}

resource "google_compute_global_address" "playfab_auth_ip" {
    name = "playfab-auth-address"
}

output "gateway_host" {
  value = "${google_compute_global_address.gateway_ip.address}"
}

output "party_host" {
  value = "${google_compute_global_address.party_ip.address}"
}

output "playfab_auth_host" {
  value = "${google_compute_global_address.playfab_auth_ip.address}"
}