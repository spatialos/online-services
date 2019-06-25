# This file contains variables. You can set them yourself (using a .tfvars file),
# or provide them when you run `terraform plan`.

# The Google Cloud project ID (not display name).
variable "gcloud_project" {}

# A gcloud region. Pick one from: https://cloud.google.com/compute/docs/regions-zones/
variable "gcloud_region" {}

# A gcloud zone. Must be within your specified region!
# Pick one from: https://cloud.google.com/compute/docs/regions-zones/
variable "gcloud_zone" {}

variable "k8s_cluster_name" {}
