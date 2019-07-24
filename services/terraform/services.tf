# This file is used to explicitly enable required Google Cloud services.

resource "google_project_service" "servicemanagement" {
  project            = "${var.gcloud_project}"
  service            = "servicemanagement.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "servicecontrol" {
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

resource "google_project_service" "cloudresourcemanager" {
  project            = "${var.gcloud_project}"
  service            = "cloudresourcemanager.googleapis.com"
  disable_on_destroy = false
}

resource "google_project_service" "iam" {
  project            = "${var.gcloud_project}"
  service            = "iam.googleapis.com"
  disable_on_destroy = false
}
