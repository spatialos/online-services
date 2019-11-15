# This file contains variables. You can set them yourself (using a .tfvars file),
# or provide them when you run `terraform plan`.

# The Google Cloud project ID (not display name).
variable "gcloud_project" {}

# A gcloud region. Pick one from: https://cloud.google.com/compute/docs/regions-zones/
variable "gcloud_region" {}

# A gcloud zone. Must be within your specified region!
# Pick one from: https://cloud.google.com/compute/docs/regions-zones/
variable "gcloud_zone" {}

# The name of your Kubernetes cluster.
variable "k8s_cluster_name" {}

# BigQuery & Cloud Storage location, either `US` or `EU`, see: https://cloud.google.com/bigquery/docs/locations#multi-regional_locations
variable "cloud_storage_location" {}
