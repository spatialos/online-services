# This file sources the available Terraform modules: one for the Gateway and one for Analytics.

# If you do not wish to deploy the gateway, comment out the Gateway Section below!

# === Start Gateway Section === #

module "gateway" {
  source            = "./module-gateway"
  gcloud_project    = "${var.gcloud_project}"
  gcloud_region     = "${var.gcloud_region}"
  gcloud_zone       = "${var.gcloud_zone}"
  k8s_cluster_name  = "${var.k8s_cluster_name}"
  container_network = "${google_compute_network.container_network.self_link}"
}

output "gateway_host" {
  value = module.gateway.gateway_host
}

output "gateway_dns" {
  value = module.gateway.gateway_dns
}

output "party_host" {
  value = module.gateway.party_host
}

output "party_dns" {
  value = module.gateway.party_dns
}

output "redis_host" {
  value = module.gateway.redis_host
}

# === End Gateway Section === #

# If you do not wish to deploy the playfab auth service, comment out the PlayFab Auth Section below!

# === Start PlayFab Auth Section === #

module "playfab_auth" {
  source           = "./module-playfab-auth"
  gcloud_project   = "${var.gcloud_project}"
  gcloud_region    = "${var.gcloud_region}"
  gcloud_zone      = "${var.gcloud_zone}"
  k8s_cluster_name = "${var.k8s_cluster_name}"
}

output "playfab_auth_host" {
  value = module.playfab_auth.playfab_auth_host
}

output "playfab_auth_dns" {
  value = module.playfab_auth.playfab_auth_dns
}

# === End PlayFab Auth Section === #

# If you do not wish to deploy analytics, comment out the Analytics Section below!

# === Start Analytics Section === #

module "analytics" {
  source                 = "./module-analytics"
  cloud_storage_location = "${var.cloud_storage_location}"
  gcloud_region          = "${var.gcloud_region}"
  gcloud_project         = "${var.gcloud_project}"
  k8s_cluster_name       = "${var.k8s_cluster_name}"
}

output "analytics_host" {
  value = module.analytics.analytics_host
}

output "analytics_dns" {
  value = module.analytics.analytics_dns
}

# === End Analytics Section === #
