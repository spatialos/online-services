# This file contains variables. You can set them yourself, or provide them when
# you run `terraform plan`.

# The Google Cloud project ID (not display name).
variable "gcloud_project" {}

# A zone (not a region); pick one from: https://cloud.google.com/compute/docs/regions-zones/
variable "gcloud_zone" {}

variable "k8s_cluster_name" {}