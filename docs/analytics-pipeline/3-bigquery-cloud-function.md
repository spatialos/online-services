# BigQuery: Cloud Function to Native Storage

The situation might arise that querying GCS as an external table, or periodically running manual imports into native BigQuery storage, no longer suit your needs. What you might want is an active ingestion stream from GCS into native BigQuery storage.

In order to facilitate this, we provide two things:

1. [A Cloud Function that picks up analytics event files that are written into a specific GCS URI prefix, and forwards them into native BigQuery storage.](#1---utilizing-the-cloud-function)
2. [A batch script to backfill analytics event files.](#2---executing-backfills)

## (1) - Utilizing the Cloud Function

When deploying [the analytics Terraform module](../../services/terraform/module-analytics), you automatically also deployed the [analytics Cloud Function (`function-gcs-to-bq-.*`)](https://console.cloud.google.com/functions/list). Whenever events are sent to our endpoint where the URL parameter **event_category** was set to **function**, a Pub/Sub notification is triggered that invokes our analytics Cloud Function to pick up this file & ingest it into native BigQuery storage. Only when a Cloud Function is invoked, do you accrue any costs.

The function:

- Provisions required BigQuery datasets & tables if they do not already exist.
- Verifies it is parsing ~ analytics events (by looking for **eventClass** in each event).
- Safely parses all expected event keys:
    + It tries parsing keys as both camelCase & snake_case.
    + It returns NULL if key not present.
    + It ignores unexpected keys.
- Augments the events with a **jobName** & an **insertedTimestamp**.
- Turns camelCase keys into snake_case table column names.
- Writes the events into an events table, a log into a logs table & in case parsing failed an error into a debug table.

In order to utilize the function, you have to make sure **event_category** is set to **function** when POST'ing your events:

```bash
curl --request POST \
  --header "content-type:application/json" \
  --data "{\"eventSource\":\"client\",\"eventClass\":\"test\",\"eventType\":\"cloud_function\",\"eventTimestamp\":1562599755,\"eventIndex\":6,\"sessionId\":\"f58179a375290599dde17f7c6d546d78\",\"buildVersion\":\"2.0.13\",\"eventEnvironment\":\"testing\",\"eventAttributes\":{\"playerId\": 12345678}}" \
  "http://analytics.endpoints.[your project id].cloud.goog:80/v1/event?key=[your gcp api key]&analytics_environment=testing&event_category=function&session_id=f58179a375290599dde17f7c6d546d78"
```

After submitting the event, verify in [the BigQuery UI](https://console.cloud.google.com/bigquery):

- There is now a dataset called `events` with table called `events_function` which contains your event.
- There is now a dataset called `logs` with a table called `events_logs_function` which contains a parse log of your event.
- The `logs` dataset will also contain two more empty tables: `events_debug_batch` & `events_logs_function_backfill`.

_Tip - Re-submit the curl request without eventClass in the --data payload (or just swap it out for random JSON) to see how errors are written into `logs.events_debug_batch`!_

## (2) - Executing Backfills

You might find yourself in the situation that there are events in GCS that you wish to ingest into native BigQuery storage using the Cloud Function. This could be because you either dropped your events table in BigQuery, or for instance did not write these events with the correct parameter setting (`..&event_category=function..`).

For these situation we provide a batch script which you can point to:

1. Files in GCS.
2. A Pub/Sub Topic that should receive notifications about the existence of these files (in our case: the Pub/Sub Topic which feeds our analytics Cloud Function).

The script is written using [Apache Beam's Python SDK](https://beam.apache.org/documentation/sdks/python/), and executed on [Cloud Dataflow](https://cloud.google.com/dataflow/). As these backfills are executed on an ad-hoc basis (only when required) we do not package it up and/or deploy it into production.

First, navigate to the [service account overview in the Cloud Console](https://console.cloud.google.com/iam-admin/serviceaccounts) and store a JSON key from the service account named **Dataflow Batch** locally on your machine + write down the file path: **[local JSON key path for Dataflow]**.

Second, as usual, let's create a virtual Python environment & install dependencies:

```bash
# Step out of your current Python 3 virtual environment, if you are in one:
deactivate

# Create a new Python 3 virtual environment:
python3 -m venv venv-dataflow

# Activate virtual environment:
source venv-dataflow/bin/activate

# Upgrade Python's package manager pip:
pip install --upgrade pip

# Install dependencies with pip:
pip install -r ../../services/python/analytics-pipeline/src/requirements/dataflow.txt
```

Now let's boot our backfill batch script:

```bash
# Set environment variable for credentials:
export GOOGLE_APPLICATION_CREDENTIALS=[local JSON key path for Dataflow]

# Trigger script:
python ../../services/python/analytics-pipeline/src/dataflow/p1-gcs-to-bq-backfill.py  \
  --setup-file=../../services/python/analytics-pipeline/src/dataflow/setup.py \ # Required
  --execution-environment=DataflowRunner \ # Required
  --local-sa-key=[local JSON key path for Dataflow] \ # Required
  --gcs-bucket=[your project id]-analytics \ # Required
  --topic=cloud-function-gcs-to-bq-topic \ # Required
  --gcp=[your project id] \ # Required
  --analytics-environment=testing \ # Optional, if omitted will pick up the following environments: {testing, development, staging, development, production, live}
  --event-category=cold \ # Required
  --event-ds-start=2019-01-01 \ # Optional, if omitted will default to: 2019-01-01
  --event-ds-stop=2020-12-31 \ # Optional, if omitted will default to: 2020-12-31
  --event-time=8-16 # Optional, if omitted will pick up all times: {0-8, 8-16, 16-24}
```

Note that we are following the GCS file tree with many of our inputs:

> gs://{gcs-bucket}/data_type=json/analytics_environment={testing|development|staging|production|live}/event_category={!function}/event_ds={yyyy-mm-dd}/event_time={0-8|8-16|16-24}/*

Check out the execution of your Dataflow Batch script in [the Dataflow Console](https://console.cloud.google.com/dataflow)!

If you pointed the backfill script to files in GCS that were **not already present in BigQuery**, verify in [the BigQuery UI](https://console.cloud.google.com/bigquery):

- There are now parse logs from your Dataflow job in `logs.events_logs_function_backfill`
- There are now parse logs from the analytics Cloud Function in `logs.events_logs_function`
- The events have been ingested into `events.events_function`

---

Next up: [(4) - Scale testing your analytics pipeline](./4-scale-test.md)
