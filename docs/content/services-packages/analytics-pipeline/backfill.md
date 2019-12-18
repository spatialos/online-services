# Analytics Pipeline: backfill
<%(TOC)%>

## Prerequisites

* This page assumes you have already deployed the Analytics Pipeline.
* Ensure you are using Python 3.7 or higher for this section.

## Overview

You might find yourself with events in Google Cloud Storage (GCS) that you want to ingest into native BigQuery storage using the Cloud Function. This could be because you either dropped your events table in BigQuery, or for instance did not `POST` these events with the correct URL parameter setting (`..&event_category=function..`).

For these situations we provide a batch script that you can point to:

* files in GCS.
* a Pub/Sub topic that should receive notifications about the existence of these files (in our case, the Pub/Sub topic that feeds our analytics Cloud Function).

The script is written using [Apache Beam's Python SDK](https://beam.apache.org/documentation/sdks/python/), and executed on [Cloud Dataflow](https://cloud.google.com/dataflow/). As you run these backfills on an ad-hoc basis (only when required) we do not package the script up and/or deploy it into production.

## Execute

1\. Navigate to the [service account overview in the Cloud Console](https://console.cloud.google.com/iam-admin/serviceaccounts).

2\. Create and store a JSON key from the service account named **Dataflow Batch** locally on your machine, and write down the file path: `{{your_local_path_json_key_for_dataflow}}`.

3\. Create a virtual Python environment and install dependencies.

Navigate to `/services/python/analytics-pipeline/src` and run through the following steps:

```sh
# Exit your current Python 3 virtual environment, if you are in one:
deactivate

# Create a new Python 3 virtual environment:
python3 -m venv venv-dataflow

# Activate the virtual environment:
source venv-dataflow/bin/activate

# Upgrade Python's package manager pip:
pip install --upgrade pip

# Install dependencies with pip:
pip install -r requirements/dataflow.txt
```

4\. Set the `GOOGLE_APPLICATION_CREDENTIALS` environment variable, which contains the path to your secret key file.

To do this, on Windows Command Prompt, run:

```
setx GOOGLE_APPLICATION_CREDENTIALS "{{your_local_path_json_key_for_dataflow}}"
```

Note that you need to start a new Command Prompt window after running this.

On other platforms, run:

```sh
export GOOGLE_APPLICATION_CREDENTIALS={{your_local_path_json_key_for_dataflow}}
```

Make sure you unset the `GOOGLE_APPLICATION_CREDENTIALS` environment variable after you finish. Otherwise, Terraform defaults to using these credentials instead of those you configured with `gcloud`.

5\. Navigate to `/services/python/analytics-pipeline/src` and execute the backfill batch script using the table and template below.

| Flag | Optional/Required | Description |
|------|-------------------|-------------|
| `--setup-file` | Required | The local path to a setup file that Dataflow requires for each worker it starts up and uses for the job. |
| `--execution-environment` | Required | Where to run your Apache Beam batch script, either DirectRunner (on your local machine) or DataflowRunner (on Cloud Dataflow). |
| `--bucket-name` | Required | The name of the GCS bucket that contains your analytics events, which will be `{{your_google_project_id}}-analytics-{{your_environment}}`. |
| `--topic` | Required | The Pub/Sub topic that the script needs to send notifications to. |
| `--location` | Required | The location of the GCS bucket that contains your analytics events, either `EU` or `US`. Use the same one chosen in `/services/terraform/terraform.tfvars`. |
| `--gcp` | Required | Your Google Cloud Project ID. |
| `--gcp-region` | Required | Region that the job will run in. Pick [a supported region](https://cloud.google.com/dataflow/docs/concepts/regional-endpoints) the same as, or close to, the region chosen in `/services/terraform/terraform.tfvars`. |
| `--environment` | Required | The environment you set while running all infrastructure with Terraform. |
| `--event-schema` | Required | To identify which files in GCS to backfill for. Either `improbable` or `playfab`. |
| `--event-category` | Required | To identify which files in GCS to backfill for. |
| `--event-environment` | Required | To identify which files in GCS to backfill for. |
| `--event-ds-start` | Optional | To identify which files in GCS to backfill for. If omitted, defaults to `2019-01-01` |
| `--event-ds-stop` | Optional | To identify which files in GCS to backfill for. If omitted, defaults to `2020-12-31` |
| `--event-time` | Optional | To identify which files in GCS to backfill for. If omitted, picks up all time periods: `00-08`, `08-16` and `16-24`. |

Note that you use the last six flags in the table above to point to files in GCS. Below you can find the start of an example path of a file stored within your analytics GCS bucket:

> gs://{{gcs_bucket_name}}/data_type=jsonl/event_schema={{improbable|playfab}}/event_category={{!native}}/event_environment={{debug|profile|release}}/event_ds={{yyyy-mm-dd}}/event_time={{00-08|08-16|16-24}}/...

```sh
python dataflow/p1_gcs_to_bq_backfill.py --setup-file=dataflow/setup.py --execution-environment=DataflowRunner --bucket-name={{your_google_project_id}}-analytics-testing --topic=cloud-function-{{improbable|playfab}}-schema-topic-testing --location={{your_analytics_bucket_location}} --gcp={{your_google_project_id}} --gcp-region={{your_google_cloud_region}} --environment=testing --event-schema={{improbable|playfab}} --event-category=external --event-environment={{debug|profile|release}}
```

View the execution of your Cloud Dataflow Batch script in [the Cloud Dataflow Console](https://console.cloud.google.com/dataflow).

If you pointed the backfill script to files in GCS that were **not already present in your native BigQuery table**, verify the following in [BigQuery](https://console.cloud.google.com/bigquery):

- There are now parse logs from your Dataflow job in `logs.dataflow_backfill_*`
- There are now parse logs from the analytics Cloud Function in `logs.events_native_*`
- The events have been ingested into `events.events_native_*`
