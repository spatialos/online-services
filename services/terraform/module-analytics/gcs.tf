# This files creates two GCS buckets.

resource "google_storage_bucket" "analytics_bucket" {
  # force_destroy = True
  name          = "${var.gcloud_project}-analytics"
  location      = var.gcloud_analytics_bucket_location
  storage_class = "MULTI_REGIONAL"
}

resource "google_storage_bucket" "functions_bucket" {
  # force_destroy = True
  name          = "${var.gcloud_project}-cloud-functions"
  location      = var.gcloud_analytics_bucket_location
  storage_class = "MULTI_REGIONAL"

  versioning {
    enabled = true
  }
}

# Note - if there are files present in your bucket while you are trying to destroy it,
# the operation will give you the following error:

# Error: Error trying to delete a bucket containing objects without `force_destroy` set to true

# To resolve this error, navigate to your bucket in the UI & manually delete all files.
# This is to ensure you are not deleting any valuable data by accident.
