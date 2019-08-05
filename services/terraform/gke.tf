# This file defines a Google Kubernetes Engine cluster.

resource "google_container_cluster" "primary" {
  name     = "${var.k8s_cluster_name}"
  location = "${var.gcloud_zone}"

  remove_default_node_pool = true
  initial_node_count = 1

  ip_allocation_policy {
    use_ip_aliases = true
  }

  master_auth {
    username = "analytics-endpoint"
    password = random_string.password.result
  }
}

resource "google_container_node_pool" "primary_preemptible_nodes" {
    name       = "${var.k8s_cluster_name}-node-pool"
    location   = "${var.gcloud_zone}"
    cluster    = "${google_container_cluster.primary.name}"
    node_count = 3

    node_config {
      preemptible  = true
      machine_type = "n1-standard-1"

      oauth_scopes = [
        "https://www.googleapis.com/auth/compute",
        "https://www.googleapis.com/auth/devstorage.read_only",
        "https://www.googleapis.com/auth/logging.write",
        "https://www.googleapis.com/auth/monitoring",
        "https://www.googleapis.com/auth/service.management.readonly",
        "https://www.googleapis.com/auth/servicecontrol",
      ]
    }

}

resource "random_string" "password" {
  length           = 16
  special          = false
  override_special = "/@\" "
}
