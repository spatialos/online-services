# BigQuery: GCS as External Data Source

This section outlines how our newline delimited JSON events in GCS can be instantly accessed with BigQuery, and parsed using SQL statements.

1. [Using Permanent BigQuery Tables](#1---using-permanent-bigquery-tables)
2. [Using Temporary BigQuery Tables](#2---using-temporary-bigquery-tables)

## (1) - Using Permanent BigQuery Tables

When a table is permanent (vs. temporary), it means that the table is persistent, and for instance always is visible & accessible in [your BigQuery overview in the UI](https://console.cloud.google.com/bigquery).

Within the permanent table class, there are two types:

- **Native** BigQuery tables, which have their data in native BigQuery storage.
- **External** BigQuery tables, which have their data outside of native BigQuery Storage.

When you deployed the Analytics Module with Terraform, you provisioned a dataset called `events` & a permanent external table called `events_gcs_external` which get its data from your analytics Google Cloud Storage bucket. By default it is pointed to `gs://[your Google project id]-analytics/data_type=json/*`, which basically means all JSON events data that is present in your bucket. You can check it out in your [BigQuery overview](https://console.cloud.google.com/bigquery). Each SQL query submitted against this table will check out all JSON files on the provided GCS URI, and therefore always include the latest data.

As the number of files in your path grows over-time, queries will take longer to execute. One solution would be to create several other external tables (either via the UI or with Terraform), where each GCS URI is further specified, reducing the overall number of files it will parse when querying a specific table (e.g. `gs://[your Google project id]-analytics/data_type=json/analytics_environment=production/*` for a `events_gcs_external_production` table).

A second upcoming solution will be the support of Hive partitioning paths, which means that when using external tables, you can further filter (beyond the source GCS URI) which files should be taken into consideration, by adding path_keys into the WHERE clause of your SQL statement (e.g. `SELECT * FROM table WHERE event_ds = '2019-06-05'` will only look at files matching both the source GCS URI **and** `../event_ds=2019-06-05/..`. This is why event keys should be camelCase whereas path partitions snake_case: so we can easily distinguish between them when writing our SQL queries.

_Tip: The GCS file path [accepts wildcards](https://cloud.google.com/bigquery/external-data-cloud-storage#wildcard-support)._

[More information about **permanent** tables..](https://cloud.google.com/bigquery/external-data-cloud-storage#permanent-tables)

## (2) - Using Temporary BigQuery Tables

Via the command line you will also be able to instantly run queries on your events data in GCS. Each query will first create a temporary table which contains all data found in the given GCS URI. Afterwards your SQL statement is executed & results printed to the console, after which the temporary table is deleted. Example query & output:

```bash
bq --location=EU query \
   --use_legacy_sql=false \
   --external_table_definition=temporary_table::buildVersion:STRING,eventType:STRING@NEWLINE_DELIMITED_JSON=gs://[your Google project id]-analytics/data_type=json/analytics_environment=testing/event_category=cold/\* \
   'SELECT buildVersion, eventType, COUNT(*) AS n FROM temporary_table GROUP BY 1, 2;'

 # Waiting on bqjob_r686b1eba38dfa29f_0000016a7404cf2a_1 ... (0s) Current status: DONE   
 # +--------------+----------------------------+------+
 # | buildVersion |         eventType          |  n   |
 # +--------------+----------------------------+------+
 # | 2.0.13       | scale-test-1000-1556724049 | 4000 |
 # +--------------+----------------------------+------+
```

[More information about **temporary** tables..](https://cloud.google.com/bigquery/external-data-cloud-storage#temporary-tables)

---

Next up: [(3) - Deploying a Cloud Function that forwards events from GCS into native BigQuery storage (as opposed to using GCS as an external data source)](./3-bigquery-cloud-function.md)
