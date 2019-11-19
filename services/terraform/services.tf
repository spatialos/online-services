# This file is used to explicitly enable required Google Cloud services.

resource "google_project_service" "service_usage" {
  project            = "${var.gcloud_project}"
  service            = "serviceusage.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "service_management" {
  project            = "${var.gcloud_project}"
  service            = "servicemanagement.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "service_control" {
  project            = "${var.gcloud_project}"
  service            = "servicecontrol.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "endpoints" {
  project            = "${var.gcloud_project}"
  service            = "endpoints.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "redis" {
  project            = var.gcloud_project
  service            = "redis.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "container" {
  project            = "${var.gcloud_project}"
  service            = "container.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "cloud_resource_manager" {
  project            = "${var.gcloud_project}"
  service            = "cloudresourcemanager.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "iam" {
  project            = "${var.gcloud_project}"
  service            = "iam.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "cloud_function" {
  project            = "${var.gcloud_project}"
  service            = "cloudfunctions.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "cloud_dataflow" {
  project            = "${var.gcloud_project}"
  service            = "dataflow.googleapis.com"
  disable_on_destroy = false
}
