# This file defines our providers. In this case, Google Cloud & Kubernetes.

# Google Cloud
provider "google" {
  project = "${var.gcloud_project}"
  zone    = "${var.gcloud_zone}"
}

# Kubernetes
provider "kubernetes" {
  host     = google_container_cluster.primary.endpoint
  username = google_container_cluster.primary.master_auth[0].username
  password = random_string.password.result

  client_certificate = base64decode(
    google_container_cluster.primary.master_auth[0].client_certificate,
  )

  client_key = base64decode(
    google_container_cluster.primary.master_auth[0].client_key,
  )

  cluster_ca_certificate = base64decode(
    google_container_cluster.primary.master_auth[0].cluster_ca_certificate,
  )
}
