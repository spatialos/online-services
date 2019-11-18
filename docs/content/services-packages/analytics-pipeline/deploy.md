# Analytics Pipeline: deploy
<%(TOC)%>

<%(Callout type="info" message="If you have already deployed any other Online Service (such as the Gateway or Deployment Pool), you have already enabled the Analytics Pipeline, which means you can skip to the [usage]({{urlRoot}}/content/services-packages/analytics-pipeline/usage) section.")%>

## Prerequisites

Set up everything listed on the [Setup]({{urlRoot}}/content/get-started/setup) page, except for .NET Core (under [Third-party tools]({{urlRoot}}/content/get-started/setup#third-party-tools)) which is not required for this section. It might be sensible to create a separate Google cloud project just for analytics, with relaxed permission settings for easy data access.

## Step 1 - Create your infrastructure

1\. Ensure your local `gcloud` tool is correctly authenticated with Google Cloud. To do this, run:
```sh
gcloud auth application-default login
```

2\. Ensure [the required APIs for your Google project are enabled](https://console.cloud.google.com/flows/enableapi?apiid=serviceusage.googleapis.com,servicemanagement.googleapis.com,servicecontrol.googleapis.com,endpoints.googleapis.com,container.googleapis.com,cloudresourcemanager.googleapis.com,iam.googleapis.com,cloudfunctions.googleapis.com,dataflow.googleapis.com). When successfully enabled, the response will look like: `Undefined parameter - API_NAMES have been enabled`.

3\. In your copy of the `online-services` repo, navigate to `/services/terraform` and create a file called `terraform.tfvars`. In this file, set the following variables:

| Variable | Description |
|----------|-------------|
| `gcloud_project` | Your cloud project ID. Note that this is the ID, not the display name. |
| `gcloud_region` | A region. Pick one from [this list](https://cloud.google.com/compute/docs/regions-zones/#available), ensuring you pick a region and not a zone (zones are within regions). |
| `gcloud_zone` | A zone. Ensure this zone is within your chosen region. For example, the zone `europe-west1-c` is within region `europe-west1`. |
| `k8s_cluster_name` | A name for your cluster. You can put whatever you like here. |
| `cloud_storage_location` | The location of your GCS buckets, either `US` or `EU`. |

The contents of your `terraform.tfvars` file should look something like:

```txt
gcloud_project         = "cosmic-abbey-186211"
gcloud_region          = "europe-west2"
gcloud_zone            = "europe-west2-b"
k8s_cluster_name       = "online-services-testing"
cloud_storage_location = "EU"
```

4\. Run `terraform init`, followed by `terraform apply -target=module.analytics`. Submit `yes` when prompted. This means only the required infrastructure for Analytics is provisioned.

<%(#Expandable title="Errors with Terraform?")%>If you ran into any errors while applying your Terraform files, first try waiting a few minutes and re-running `terraform apply -target=module.analytics` followed by `yes` when prompted.<br/><br/>
If this does not solve your issue(s), inspect the printed error logs to resolve.
<%(/Expandable)%>

## Step 2 - Build your service image

You need to use Docker to build your service as a container, then push it up to your Google Cloud project’s container registry. To start, you need to configure Docker to talk to Google.

1\. Run the following commands in order:

```sh
gcloud components install docker-credential-gcr
gcloud auth configure-docker
gcloud auth login
```

Now you can build and push the Docker image for your service.

2\. Navigate to the directory where the Dockerfiles are kept (`/services/docker`).

3\. Build the image like this, replacing `{{your_google_project_id}}` with the name of your Google Cloud project:

```sh
docker build --file ./analytics-endpoint/Dockerfile --tag "gcr.io/{{your_google_project_id}}/analytics-endpoint" ..
```
4\. Once you’ve built the image, push it up to the cloud:

```sh
docker push "gcr.io/{{your_google_project_id}}/analytics-endpoint"
```

Have a look at your [container registry on the Google Cloud Console](https://console.cloud.google.com/gcr) - you should see your built image there.

## Step 3 - Set up Kubernetes

Kubernetes (or “k8s”) is configured using a tool called `kubectl`. Make sure you [have it installed]({{urlRoot}}/content/get-started/setup#third-party-tools).

Before you do anything else, you need to connect to your Google Kubernetes Engine (GKE) cluster. The easiest way to do this is to go to the [GKE page](https://console.cloud.google.com/kubernetes/list) in your Cloud Console and click **Connect**:

![]({{assetRoot}}img/services-packages/analytics-pipeline/gke-connect.png)

This gives you a `gcloud` command you can paste into your shell and run. You can verify you're connected by running `kubectl cluster-info` - you'll see some information about the Kubernetes cluster you're now connected to.

### 3.1 - Store your secret

There is one secret you need to store on Kubernetes: an API key for the Analytics Pipeline endpoint.

> A "secret" is the k8s way of storing sensitive information such as passwords and API keys. It means the information isn't stored in any configuration file or - even worse - your source control, but ensures your services still have access to the information they need.

1\. Navigate to [the API credentials overview page for your project in the Cloud Console](https://console.cloud.google.com/apis/credentials) and create a new API key. Note that newly created API keys can take up to 10 minutes before they become fully functional.

2\. Under “API restrictions”, select "Restrict key" and then choose ”Analytics REST API”.

3\. Next, mount the API key into Kubernetes as a secret, replacing `{{your_analytics_api_key}}` with the API key you just created:

```sh
kubectl create secret generic "analytics-api-key" --from-literal="analytics-api-key={{your_analytics_api_key}}"
```

### 3.2 - Edit configuration files

Now you need to edit the Kubernetes configuration files in `/services/k8s/analytics-endpoint` with variables that are specific to your deployment, such as your Google Project ID and the external IP address of your analytics endpoint. The IP address was provided when you applied your Terraform configuration (or navigate into `/services/terraform` and run `terraform output` to view it again), but you can also obtain it from the ([External IP addresses](https://console.cloud.google.com/networking/addresses/list)) page in the Google Cloud Console.

| Name | Description | Example value |
|------|-------------|---------------|
| `{{your_google_project_id}}` | The ID of your Google Cloud project. | `cosmic-abbey-186211` |
| `{{your_analytics_host}}` | The IP address of your analytics service. | `35.235.50.182` |

### 3.3 - Deploy to Google Cloud Platform

Navigate to the `/services/k8s` directory and run:

```sh
kubectl apply -Rf analytics-endpoint/
```

You can then check your [Kubernetes Workloads page](https://console.cloud.google.com/kubernetes/workload) and watch as your analytics deployment goes green. Congratulations - you've deployed the Analytics Pipeline successfully!
