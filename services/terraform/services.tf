# This file is used to explicitly enable required Google Cloud services and define API endpoints.

resource "google_project_service" "servicemanagement" {
  project = "${var.gcloud_project}"
  service = "servicemanagement.googleapis.com"
}

resource "google_project_service" "servicecontrol" {
  project = "${var.gcloud_project}"
  service = "servicecontrol.googleapis.com"
}

resource "google_project_service" "endpoints" {
  project = "${var.gcloud_project}"
  service = "endpoints.googleapis.com"
}

resource "google_endpoints_service" "deployment_metadata_endpoint" {
  service_name         = "deployment-metadata.endpoints.${var.gcloud_project}.cloud.goog"
  project              = "${var.gcloud_project}"
  grpc_config          = "${templatefile("./spec/deployment_metadata_spec.yml", { project: var.gcloud_project, target: google_compute_address.deployment_metadata_ip.address })}"
  protoc_output_base64 = "${filebase64("./api_descriptors/deployment_metadata_descriptor.pb")}"
}

resource "google_endpoints_service" "gateway_endpoint" {
  service_name         = "gateway.endpoints.${var.gcloud_project}.cloud.goog"
  project              = "${var.gcloud_project}"
  grpc_config          = "${templatefile("./spec/gateway_spec.yml", { project: var.gcloud_project, target: google_compute_address.gateway_ip.address })}"
  protoc_output_base64 = "${filebase64("./api_descriptors/gateway_descriptor.pb")}"
}

resource "google_endpoints_service" "party_endpoint" {
  service_name         = "party.endpoints.${var.gcloud_project}.cloud.goog"
  project              = "${var.gcloud_project}"
  grpc_config          = "${templatefile("./spec/party_spec.yml", { project: var.gcloud_project, target: google_compute_address.party_ip.address })}"
  protoc_output_base64 = "${filebase64("./api_descriptors/party_descriptor.pb")}"
}

resource "google_endpoints_service" "playfab_auth_endpoint" {
  service_name         = "playfab-auth.endpoints.${var.gcloud_project}.cloud.goog"
  project              = "${var.gcloud_project}"
  grpc_config          = "${templatefile("./spec/playfab_auth_spec.yml", { project: var.gcloud_project, target: google_compute_address.playfab_auth_ip.address })}"
  protoc_output_base64 = "${filebase64("./api_descriptors/playfab_auth_descriptor.pb")}"
}
