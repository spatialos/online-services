# This file contains the creation of a BigQuery dataset & table, which uses Google
# Cloud Storage as an external data source (vs. having data in native BigQuery storage).

resource "google_bigquery_dataset" "dataset_events" {
  dataset_id    = "events"
  friendly_name = "events"
  description   = "Dataset which contains all analytics events."
  location      = var.gcloud_bucket_location
}

resource "google_bigquery_table" "table_events_gcs_external" {
  dataset_id = "${google_bigquery_dataset.dataset_events.dataset_id}"
  table_id   = "events_gcs_external"

  external_data_configuration {
    autodetect            = false
    compression           = "GZIP"
    ignore_unknown_values = true
    source_format         = "NEWLINE_DELIMITED_JSON"

    source_uris = [
      "gs://${var.gcloud_project}-analytics/data_type=json/*",
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
  "description": "The environment the event originated from."
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
  "description": "MD5 hash of the GCS filepath."
},
{
  "name": "eventId",
  "type": "STRING",
  "mode": "NULLABLE",
  "description": "MD5 hash of the GCS filepath + '/{event_index_in_batch}'."
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
  "description": "The timestamp of the event."
},
{
  "name": "receivedTimestamp",
  "type": "TIMESTAMP",
  "mode": "NULLABLE",
  "description": "The timestamp of when the event was received."
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


resource "google_bigquery_table" "table_events_gcs_external_os" {
  dataset_id = "${google_bigquery_dataset.dataset_events.dataset_id}"
  table_id   = "events_gcs_external_os"

  external_data_configuration {
    autodetect            = false
    compression           = "GZIP"
    ignore_unknown_values = true
    source_format         = "NEWLINE_DELIMITED_JSON"

    source_uris = [
      "gs://${var.gcloud_project}-analytics/data_type=json/analytics_environment=testing/event_category=online_services/*",
      "gs://${var.gcloud_project}-analytics/data_type=json/analytics_environment=staging/event_category=online_services/*",
      "gs://${var.gcloud_project}-analytics/data_type=json/analytics_environment=production/event_category=online_services/*",
      "gs://${var.gcloud_project}-analytics/data_type=json/analytics_environment=live/event_category=online_services/*"
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
    "description": "The environment the event originated from."
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
    "description": "MD5 hash of the GCS filepath."
  },
  {
    "name": "eventId",
    "type": "STRING",
    "mode": "NULLABLE",
    "description": "MD5 hash of the GCS filepath + '/{event_index_in_batch}'."
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
    "description": "The timestamp of the event."
  },
  {
    "name": "receivedTimestamp",
    "type": "TIMESTAMP",
    "mode": "NULLABLE",
    "description": "The timestamp of when the event was received."
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
