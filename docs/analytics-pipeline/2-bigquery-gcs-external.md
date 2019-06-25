# BigQuery: GCS as External Data Source

This section outlines how our newline delimited JSON events in GCS can be instantly accessed with BigQuery, and parsed using SQL statements.

1. [Using Permanent BigQuery Tables](#1---using-permanent-bigquery-tables)
2. [Using Temporary BigQuery Tables](#2---using-temporary-bigquery-tables)

## (1) - Using Permanent BigQuery Tables

Go to your [BigQuery overview](https://console.cloud.google.com/bigquery), click on (or create) a dataset that has the same region as your GCS bucket ([check](https://console.cloud.google.com/storage/)) & then **Create Table**. Use the example input below (or tweak as appropriate to your situation) to fill out the form:

Either choose a **Native BigQuery Table** (static import of GCS data):

- Table type: **Native** - which imports the data "as of now" into a static table in native BigQuery storage.
- Table name: `events_gcs_native_static`
- Partition and cluster settings:
    + Partitioning: **By field:** `eventTimestamp`
    + Clustering order: `eventClass,eventType` (optional)
- Write preference, choose one of: {Write if empty, Append to table, Overwrite table}.

Or an **External BigQuery Table** (live link with GCS data):

- Table type: **External** to establish a live link between GCS & BigQuery.
- Table name: `events_gcs_external_live`

> Note that querying an External table will dynamically (re-)parse all files present in your GCS URI. Therefore, as the number of files in your path grows over-time, queries will take longer to execute. An upcoming feature will be the support of Hive partitioning paths, which means that when using External tables, you can further filter (beyond the source GCS URI) which files should be taken into consideration, by adding path_keys into the WHERE clause of your SQL statement (e.g. `SELECT * FROM table WHERE event_ds = '2019-06-05'` will only look at files matching both the GCS URI **and** `../event_ds=2019-06-05/..`. This is why event keys should be camelCase whereas path partitions snake_case: so we can easily distinguish between them when writing our SQL queries.

For both External & Native tables the following settings can be identical:

- Create table from: **Google Cloud Storage**
- Select file from GCS bucket: `gs://[your project id]-analytics/data_type=json/analytics_environment=testing/event_category=cold/*`
- File format: **JSON (Newline-delimited)**
- Schema (select **Edit as text**): `eventEnvironment:STRING,batchId:STRING,eventId:STRING,eventClass:STRING,eventType:STRING,sessionId:STRING,eventSource:STRING,eventIndex:INTEGER,buildVersion:STRING,eventTimestamp:TIMESTAMP,receivedTimestamp:TIMESTAMP,eventAttributes:STRING`
- Under **Advanced options** select **Ignore unknown values**

Now hit **Create table** again!

The following usage notes apply:

- Values denoted in the **schema** that are **not present in the event** JSON dictionary in GCS will be shown as **NULL in BigQuery**.
- If you want to omit importing certain event attributes by excluding them from the schema, you **must** select **Ignore unknown values**.

_Tip: The GCS file path [accepts wildcards](https://cloud.google.com/bigquery/external-data-cloud-storage#wildcard-support)._

[More information about **permanent** tables..](https://cloud.google.com/bigquery/external-data-cloud-storage#permanent-tables)

## (2) - Using Temporary BigQuery Tables

Via the command line you will be able to instantly run queries on your events data in GCS. Each query will first create a temporary table which contains all data found in the given GCS URI. Afterwards your SQL statement is executed & results printed to the console, after which the temporary table is deleted. Example query & output:

```bash
bq --location=EU query \
   --use_legacy_sql=false \
   --external_table_definition=temporary_table::buildVersion:STRING,eventType:STRING@NEWLINE_DELIMITED_JSON=gs://[your project id]-analytics/data_type=json/analytics_environment=testing/event_category=cold/\* \
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
