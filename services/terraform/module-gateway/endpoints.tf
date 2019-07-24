# This file creates the endpoints required for the Gateway.

resource "google_endpoints_service" "gateway_endpoint" {
  service_name         = "gateway.endpoints.${var.gcloud_project}.cloud.goog"
  project              = "${var.gcloud_project}"
  grpc_config          = "${templatefile("./module-gateway/spec/gateway_spec.yml", { project: var.gcloud_project, target: google_compute_address.gateway_ip.address })}"
  protoc_output_base64 = "${filebase64("./module-gateway/api_descriptors/gateway_descriptor.pb")}"
}

resource "google_endpoints_service" "party_endpoint" {
  service_name         = "party.endpoints.${var.gcloud_project}.cloud.goog"
  project              = "${var.gcloud_project}"
  grpc_config          = "${templatefile("./module-gateway/spec/party_spec.yml", { project: var.gcloud_project, target: google_compute_address.party_ip.address })}"
  protoc_output_base64 = "${filebase64("./module-gateway/api_descriptors/party_descriptor.pb")}"
}

resource "google_endpoints_service" "playfab_auth_endpoint" {
  service_name         = "playfab-auth.endpoints.${var.gcloud_project}.cloud.goog"
  project              = "${var.gcloud_project}"
  grpc_config          = "${templatefile("./module-gateway/spec/playfab_auth_spec.yml", { project: var.gcloud_project, target: google_compute_address.playfab_auth_ip.address })}"
  protoc_output_base64 = "${filebase64("./module-gateway/api_descriptors/playfab_auth_descriptor.pb")}"
}

# Note - if you recently applied & tore down your endpoints, and you are trying to re-apply them within 30 days, you might get the following error:

# Error: googleapi: Error 400: Service analytics.endpoints.{GCLOUD_PROJECT_ID}.cloud.goog has been deleted and will be purged after 30 days.
# To reuse this service, please undelete the service following https://cloud.google.com/service-management/create-delete., failedPrecondition

# If this is the case, first undelete the endpoint before re-applying it with Terraform, by running:
# `gcloud endpoints services undelete analytics.endpoints.{GCLOUD_PROJECT_ID}.cloud.goog`

output "gateway_dns" {
  value = "${google_endpoints_service.gateway_endpoint.dns_address}"
}

output "party_dns" {
  value = "${google_endpoints_service.party_endpoint.dns_address}"
}

output "playfab_auth_dns" {
  value = "${google_endpoints_service.playfab_auth_endpoint.dns_address}"
}
