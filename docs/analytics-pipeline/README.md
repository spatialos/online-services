# The Analytics Pipeline

Before you begin..

- The [quickstart](../quickstart.md) lists [several prerequisites you'll need to install](../quickstart.md#prerequisites) in order to run through this section, as well as [how to configure gcloud with Docker](../quickstart.md#building-your-service-images).
- Make sure you have applied the [analytics Terraform module](../../services/terraform/module-analytics), to ensure all required resources for this section have been provisioned.
    + To do so, navigate into [the Terraform folder](../../services/terraform), ensure that the analytics section within [modules.tf](../../services/terraform/modules.tf) is not commented out & run `terraform init` followed by `terraform apply`.
- Afterwards, navigate to the [service account overview in the Cloud Console](https://console.cloud.google.com/iam-admin/serviceaccounts) to create a few local service account keys which you will need later:
    + Store both a JSON & p12 key from the service account named **Analytics GCS Writer** locally on your machine + write down the file paths: **[local JSON key path writer]**, **[local p12 key path writer]**.
    + Store a JSON key from the service account named **Analytics Endpoint** locally on your machine + write down the file path: **[local JSON key path writer endpoint]**.
- The `gcloud` cli usually ships with a Python 2 interpreter, whereas we will use Python 3. Run `gcloud topic startup` for [more information](https://cloud.google.com/sdk/install) on how to point `gcloud` to a Python 3.4+ interpreter. Otherwise you could use something like [pyenv](https://github.com/pyenv/pyenv) to toggle between Python versions.

There are currently 4 parts to the analytics pipeline documentation:

1. [Creating an endpoint to POST your analytics events to](./1-cloud-endpoint.md) [**required**].
2. [Using Google Cloud Storage (GCS) as an external data source through BigQuery](./2-bigquery-gcs-external.md) [**required**].
3. [Deploying a Cloud Function that forwards events from GCS into native BigQuery storage](./3-bigquery-cloud-function.md) [**optional**].
4. [Scale testing your analytics pipeline](./4-scale-test.md) [**optional**].
