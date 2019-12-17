# This file contains the creation of a BigQuery dataset & table, which uses Google
# Cloud Storage as an external data source (vs. having data in native BigQuery storage).

resource "google_bigquery_dataset" "dataset_general" {
  dataset_id    = "general"
  friendly_name = "Improbable Schema"
  description   = "Dataset containing analytics events adhering to Improbable's schema."
  location      = var.cloud_storage_location
}

resource "google_bigquery_dataset" "dataset_playfab" {
  dataset_id    = "playfab"
  friendly_name = "PlayFab Schema"
  description   = "Dataset containing analytics events adhering to PlayFab's schema."
  location      = var.cloud_storage_location
}

resource "google_bigquery_table" "table_events_gcs_external_general" {
  dataset_id = google_bigquery_dataset.dataset_general.dataset_id
  table_id   = "events_gcs_external"

  external_data_configuration {
    autodetect            = false
    compression           = "GZIP"
    ignore_unknown_values = true
    source_format         = "NEWLINE_DELIMITED_JSON"

    source_uris = [
      "gs://${var.gcloud_project}-analytics/data_type=json/analytics_environment=development/event_category=function/*",
      "gs://${var.gcloud_project}-analytics/data_type=json/analytics_environment=testing/event_category=function/*",
      "gs://${var.gcloud_project}-analytics/data_type=json/analytics_environment=staging/event_category=function/*",
      "gs://${var.gcloud_project}-analytics/data_type=json/analytics_environment=production/event_category=function/*"
    ]
  }

  schema = <<EOF
[
{
  "name": "analyticsEnvironment",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "Environment derived from the GCS path."
},
{
  "name": "eventEnvironment",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "The build configuration that the event was sent from, e.g. {debug, profile, release}."
},
{
  "name": "eventSource",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "Type of the worker the event originated from."
},
{
  "name": "sessionId",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "The session ID, which is unique per client/server worker session."
},
{
  "name": "versionId",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "The version of the game build or online service."
},
{
  "name": "batchId",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "MD5 hexdigest of the GCS filepath."
},
{
  "name": "eventId",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "MD5 hexdigest of the GCS filepath + '/{event_index_in_batch}'."
},
{
  "name": "eventIndex",
  "type": "INTEGER",
  "mode": "NULLABLE",
  "description": "The index of the event within its batch."
},
{
  "name": "eventClass",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "Higher order category of event type."
},
{
  "name": "eventType",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "The event type."
},
{
  "name": "playerId",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "A player's unique identifier, if available."
},
{
  "name": "eventTimestamp",
  "type": "TIMESTAMP",
  "mode": "NULLABLE",
  "description": "The UTC timestamp when the event took place."
},
{
  "name": "receivedTimestamp",
  "type": "TIMESTAMP",
  "mode": "NULLABLE",
  "description": "The UTC timestamp when the event was received."
},
{
  "name": "eventAttributes",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "Custom data for the event."
}
]
EOF
}

resource "google_bigquery_table" "table_events_gcs_external_playfab" {
  dataset_id = google_bigquery_dataset.dataset_playfab.dataset_id
  table_id   = "events_gcs_external"

  external_data_configuration {
    autodetect            = false
    compression           = "GZIP"
    ignore_unknown_values = true
    source_format         = "NEWLINE_DELIMITED_JSON"

    source_uris = [
      "gs://${var.gcloud_project}-analytics/data_type=json/analytics_environment=development/event_category=playfab/*",
      "gs://${var.gcloud_project}-analytics/data_type=json/analytics_environment=testing/event_category=playfab/*",
      "gs://${var.gcloud_project}-analytics/data_type=json/analytics_environment=staging/event_category=playfab/*",
      "gs://${var.gcloud_project}-analytics/data_type=json/analytics_environment=production/event_category=playfab/*"
    ]
  }

  schema = <<EOF
[
{
  "name": "AnalyticsEnvironment",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "Environment derived from the GCS path."
},
{
  "name": "PlayFabEnvironment",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "Your PlayFab environment."
},
{
  "name": "SourceType",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "The type of source of this event (PlayFab partner, other backend, or from the PlayFab API)."
},
{
  "name": "Source",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "The name of the source of this PlayStream event."
},
{
  "name": "EventNamespace",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "The assigned namespacing for this event. For example: 'com.myprogram.ads'"
},
{
  "name": "TitleId",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "The ID of your PlayFab title."
},
{
  "name": "BatchId",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "MD5 hexdigest of the GCS filepath."
},
{
  "name": "EventId",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "PlayFab event ID."
},
{
  "name": "EventName",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "The name of this event."
},
{
  "name": "EntityType",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "The type of entity (player, title, etc.) to which this event applies."
},
{
  "name": "EntityId",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "The identifier for the entity (title, player, etc) to which this event applies."
},
{
  "name": "Timestamp",
  "type": "TIMESTAMP",
  "mode": "NULLABLE",
  "description": "The UTC timestamp when the event took place."
},
{
  "name": "ReceivedTimestamp",
  "type": "TIMESTAMP",
  "mode": "NULLABLE",
  "description": "The UTC timestamp when the event was received."
},
{
  "name": "EventAttributes",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "Custom data for the event."
}
]
EOF
}
