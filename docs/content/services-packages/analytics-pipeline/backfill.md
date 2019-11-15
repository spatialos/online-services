# Analytics Pipeline: backfill
<%(TOC)%>

## Prerequisites

* This page assumes you have already deployed the Analytics Pipeline.
* Ensure you are using Python 3 for this section.

## Overview

You might find yourself with events in Google Cloud Storage (GCS) that you want to ingest into native BigQuery storage using the Cloud Function. This could be because you either dropped your events table in BigQuery, or for instance did not `POST` these events with the correct URL parameter setting (`..&event_category=function..`).

For these situations we provide a batch script that you can point to:

* files in GCS.
* a Pub/Sub topic that should receive notifications about the existence of these files (in our case, the Pub/Sub topic that feeds our analytics Cloud Function).

The script is written using [Apache Beam's Python SDK](https://beam.apache.org/documentation/sdks/python/), and executed on [Cloud Dataflow](https://cloud.google.com/dataflow/). As you run these backfills on an ad-hoc basis (only when required) we do not package the script up and/or deploy it into production.

## Execute

0. Navigate to the [service account overview in the Cloud Console](https://console.cloud.google.com/iam-admin/serviceaccounts).

0. Create and store a JSON key from the service account named **Dataflow Batch** locally on your machine, and write down the file path: `{{your_local_path_json_key_for_dataflow}}`.

0. Create a virtual Python environment and install dependencies.

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

0. Set the `GOOGLE_APPLICATION_CREDENTIALS` environment variable, which contains the path to your secret key file.

To do this, on Windows Command Prompt, run:

```bat
setx GOOGLE_APPLICATION_CREDENTIALS "{{your_local_path_json_key_for_dataflow}}"
```

Note that you need to start a new Command Prompt window after running this.

On other platforms, run:

```sh
export GOOGLE_APPLICATION_CREDENTIALS={{your_local_path_json_key_for_dataflow}}
```

Make sure you unset the `GOOGLE_APPLICATION_CREDENTIALS` environment variable after you finish.Otherwise, Terraform defaults to using these credentials instead of those you configured with `gcloud`.

0. Navigate to `/services/python/analytics-pipeline/src` and execute the backfill batch script using the table and template below.

| Flag | Optional/Required | Description |
|------|-------------------|-------------|
| `--setup-file` | Required | The local path to a setup file that Dataflow requires for each worker it starts up and uses for the job. |
| `--execution-environment` | Required | Where to run your Apache Beam batch script, either DirectRunner (on your local machine) or DataflowRunner (on Cloud Dataflow). |
| `--local-sa-key` | Required | The local path to your Dataflow Batch service account key file. |
| `--bucket-name` | Required | The name of the GCS bucket that contains your analytics events, which will be `{{your_google_project_id}}-analytics`. |
| `--topic` | Required | The Pub/Sub topic that the script needs to send notifications to. |
| `--location` | Required | The location of the GCS bucket that contains your analytics events, either `EU` or `US`. Use the same one chosen in `/services/terraform/terraform.tfvars`. |
| `--gcp` | Required | Your Google Cloud Project ID. |
| `--gcp-region` | Region that the job will run in. Pick [a supported region](https://cloud.google.com/dataflow/docs/concepts/regional-endpoints) the same as, or close to, the region chosen in `/services/terraform/terraform.tfvars`. |
| `--analytics-environment` | Optional | To identify which files in GCS to backfill for. If omitted, defaults to all of the following environments: `testing`, `development`, `staging`, `development`, `production` and `live`. |
| `--event-category` | Required | To identify which files in GCS to backfill for. |
| `--event-ds-start` | Optional | To identify which files in GCS to backfill for. If omitted, defaults to `2019-01-01` |
| `--event-ds-stop` | Optional | To identify which files in GCS to backfill for. If omitted, defaults to `2020-12-31` |
| `--event-time` | Optional | To identify which files in GCS to backfill for. If omitted, picks up all time periods: `0-8`, `8-16` and `16-24`. |

Note that you use the last five flags in the table above to point to files in GCS. Below you can find an example path of a file stored within your analytics GCS bucket:

> gs://{{gcs_bucket_name}}/data_type=json/analytics_environment={{testing|development|staging|production|live}}/event_category={{!function}}/event_ds={{yyyy-mm-dd}}/event_time={{0-8|8-16|16-24}}/*

```sh
python dataflow/p1_gcs_to_bq_backfill.py --setup-file=dataflow/setup.py --execution-environment=DataflowRunner --local-sa-key={{your_local_path_json_key_for_dataflow}} --bucket-name={{your_google_project_id}}-analytics --topic=cloud-function-gcs-to-bq-topic --location={{your_analytics_bucket_location}} --gcp={{your_google_project_id}} --gcp-region={{your_google_cloud_region}} --analytics-environment=testing --event-category=cold
```

View the execution of your Cloud Dataflow Batch script in [the Cloud Dataflow Console](https://console.cloud.google.com/dataflow).

If you pointed the backfill script to files in GCS that were **not already present in your native BigQuery table**, verify the following in [BigQuery](https://console.cloud.google.com/bigquery):

- There are now parse logs from your Dataflow job in `logs.events_logs_dataflow_backfill`
- There are now parse logs from the analytics Cloud Function in `logs.events_logs_function_native`
- The events have been ingested into `events.events_function_native`
