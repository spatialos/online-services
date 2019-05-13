# This file defines our cloud provider - in this case, Google Cloud.

provider "google" {
  project = "${var.gcloud_project}"
  zone    = "${var.gcloud_zone}"
}