# This file creates a Pub/Sub topic, alongside several GCS notification that trigger
# Pub/Sub messages to be sent to our topic whenever files are created in GCS on specific
# prefixes (file paths).

# Create Pub/Sub Topics.
resource "google_pubsub_topic" "cloud_function_improbable_schema" {
  name = "cloud-function-improbable-schema-topic-${var.environment}"
}

resource "google_pubsub_topic" "cloud_function_playfab_schema" {
  name = "cloud-function-playfab-schema-topic-${var.environment}"
}

# Enable notifications by giving the correct IAM permission to the unique service account.
data "google_storage_project_service_account" "gcs_account" {}

resource "google_pubsub_topic_iam_member" "member_cloud_function_improbable_schema" {
    topic  = google_pubsub_topic.cloud_function_improbable_schema.name
    role   = "roles/pubsub.publisher"
    member = "serviceAccount:${data.google_storage_project_service_account.gcs_account.email_address}"
}

resource "google_pubsub_topic_iam_member" "member_cloud_function_playfab_schema" {
    topic  = google_pubsub_topic.cloud_function_playfab_schema.name
    role   = "roles/pubsub.publisher"
    member = "serviceAccount:${data.google_storage_project_service_account.gcs_account.email_address}"
}

# Create GCS to Pub/Sub Topic Notifications.
resource "google_storage_notification" "notifications_improbable_schema" {

  depends_on = [
    google_pubsub_topic_iam_member.member_cloud_function_improbable_schema,
    google_storage_bucket.analytics_bucket
  ]

  bucket             = "${var.gcloud_project}-analytics-${var.environment}"
  payload_format     = "JSON_API_V1"
  topic              = google_pubsub_topic.cloud_function_improbable_schema.id
  # See other event_types here: https://cloud.google.com/storage/docs/pubsub-notifications#events
  event_types        = ["OBJECT_FINALIZE"]
  # Only trigger a message to Pub/Sub for files hitting this prefix:
  object_name_prefix = "data_type=json/event_schema=improbable/event_category=native/"
}

resource "google_storage_notification" "notifications_playfab_schema" {

  depends_on = [
    google_pubsub_topic_iam_member.member_cloud_function_playfab_schema,
    google_storage_bucket.analytics_bucket,
    google_storage_notification.notifications_improbable_schema
  ]

  bucket             = "${var.gcloud_project}-analytics-${var.environment}"
  payload_format     = "JSON_API_V1"
  topic              = google_pubsub_topic.cloud_function_playfab_schema.id
  # See other event_types here: https://cloud.google.com/storage/docs/pubsub-notifications#events
  event_types        = ["OBJECT_FINALIZE"]
  # Only trigger a message to Pub/Sub for files hitting this prefix:
  object_name_prefix = "data_type=json/event_schema=playfab/event_category=native/"
}
