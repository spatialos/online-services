# Deployment Pool: deploy
<%(TOC)%>

## Prerequisites

0. Set up everything listed on the [Setup]({{urlRoot}}/content/get-started/setup) page.

## Configuration

The Deployment Pool requires information about the deployments it is going to start. Most of this information you pass as flags to the Deployment Pool when you initiate it. The full list of flags is as follows:

| Flag | Required/Optional | Purpose |
|------|-------------------|---------|
| `--deployment-prefix` | `Optional` | The Deployment Pool creates deployments and allocates them  random names. Use this parameter to add a custom prefix to all these deployment names, for example `pool-`. |
| `--minimum-ready-deployments` | `Optional` | The number of "ready-to-go" deployments to maintain. Defaults to 3. |
| `--match-type` | `Required` | A string representing the type of deployment this Pool will look after. For example, `fps`, `session`, `dungeon0`. |
| `--project` | `Required` | The SpatialOS project to start deployments in. |
| `--snapshot` | `Required` | The absolute path inside the container to the deployment snapshot to start any deployments with. |
| `--launch-config` | `Required` | The path to the launch configuration JSON file to start any deployments with. |
| `--assembly-name` | `Required` | The name of the previously uploaded assembly within the SpatialOS project this Pool is running against. |
| `--analytics.endpoint` | `Optional` | Should be `http://analytics.endpoints.{{your_google_project_id}}.cloud.goog:80/v1/event` with your own Google Cloud project ID inserted. |
| `--analytics.allow-insecure-endpoint` | `Optional` | If using an HTTP endpoint (which you are by default), this is required. |
| `--analytics.config-file-path` | `Optional` | Path to the analytics event configuration file, by default `/config/online-services-analytics-config.yaml`. |
| `--analytics.gcp-key-path` | `Optional` | Path to your Analytics REST API key, by default `/secrets/analytics-api-key`. |
| `--event.environment` | `Optional` | What you determine to be the environment of the endpoint you are deploying, for example one of `testing`, `staging` or `production`. |
| `--event.schema` | `Optional` | The schema of the events the service is sending. If you don't set this, `improbable` will be used. |

The final five flags are to configure the out-of-the-box [instrumentation](https://en.wikipedia.org/wiki/Instrumentation_(computer_programming)) that comes with the Deployment Pool. If any of these are missing, the Deployment Pool will still function, but it won’t capture any analytics events.

Finally, the Deployment Pool requires you to set a `SPATIAL_REFRESH_TOKEN` environment variable containing a SpatialOS refresh token, which provides authentication so that the Deployment Pool can use the SpatialOS Platform. You’ll create this token in [step 4.3.1]({{urlRoot}}/content/services-packages/deployment-pool/deploy#4-3-1-spatialos-refresh-token).

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
| `cloud_storage_location` | The location of your Google Cloud Storage (GCS) buckets, either `US` or `EU`. |

The contents of your `terraform.tfvars` file should look something like:

```txt
gcloud_project         = "cosmic-abbey-186211"
gcloud_region          = "europe-west2"
gcloud_zone            = "europe-west2-b"
k8s_cluster_name       = "io-online-services"
cloud_storage_location = "EU"
environment            = "testing"
```

4\. Run `terraform init`, followed by `terraform apply -target=module.analytics`. Submit `yes` when prompted. Because you set `-target` to `module.analytics`, this will only provision the required infrastructure for the Deployment Pool (which only requires the base infrastructure) and the Analytics Pipeline (for tracking the Deployment Pool).

<%(#Expandable title="Errors with Terraform?")%>If you ran into any errors while applying your Terraform files, first try waiting a few minutes and re-running `terraform apply -target=module.analytics` followed by `yes` when prompted.<br/><br/>
If this does not solve your issue(s), inspect the printed error logs to resolve. <%(/Expandable)%>

## Step 2 - Build your service images

You need to use Docker to build your services as containers, then push them up to your Google Cloud project’s container registry. To start, you need to configure Docker to talk to Google.

1\. Run the following commands in order:

```sh
gcloud components install docker-credential-gcr
gcloud auth configure-docker
gcloud auth login
```

Now you can build and push the Docker images for your services.

2\. Navigate to the directory where the Dockerfiles are kept (`/services/docker`).

3\. Build the images like this, replacing `{{your_google_project_id}}` with the name of your Google Cloud project:

```sh
docker build -f ./deployment-pool/Dockerfile -t "gcr.io/{{your_google_project_id}}/deployment-pool" ..
docker build -f ./analytics-endpoint/Dockerfile -t "gcr.io/{{your_google_project_id}}/analytics-endpoint" ..
```

4\. Once you’ve built the images, push them up to the cloud:

```sh
docker push "gcr.io/{{your_google_project_id}}/deployment-pool"
docker push "gcr.io/{{your_google_project_id}}/analytics-endpoint"
```

Have a look at your [container registry on the Google Cloud Console](https://console.cloud.google.com/gcr) - you should see your built images there.

## Step 3 - Create and upload a SpatialOS assembly

To start a deployment, you need to create and upload an assembly. You can do this with the SpatialOS CLI from within the folder that contains your SpatialOS project files. See <%(LinkTo title="`spatial upload`" doctype="reference" path="/shared/spatial-cli/spatial-upload")%> for details.

Create and upload your assembly to SpatialOS ahead of time so that the pool can access it when it starts deployments.

To do this, run:

```sh
spatial upload {{assembly_name}}
```

You can choose whatever assembly name you want. You will need to pass the string to the Deployment Pool later: `--assembly-name`.

<%(#Expandable title="Don’t have a SpatialOS assembly to hand?")%>
The quickest way to create and upload a SpatialOS assembly:<br/><br/>
<li>Clone https://github.com/spatialos/CsharpBlankProject and navigate into root.</li>
<li>Set the `project_name` field in `spatialos.json` to match your SpatialOS project name.</li>
<li>Create and upload an assembly by running `spatial cloud upload {{assembly_name}}`. You can choose whatever assembly name you want. You will need to pass the `{{assembly_name}}` string to the Deployment Pool later: `--assembly-name`.</li>
<br/><br/>Particular files you will need later on are `/default_launch.json` and `/snapshots/default.snapshot`.<br/><br/>Disclaimer: We do not officially support the CSharpBlankProject and it can change at any time.<%(/Expandable)%>

## Step 4 - Set up Kubernetes

Kubernetes (or **k8s**) is configured using a tool called `kubectl`. Make sure you [have it installed]({{urlRoot}}/content/get-started/setup#third-party-tools).

Before you do anything else you need to connect to your GKE cluster. The easiest way to do this is to go to the [GKE page](https://console.cloud.google.com/kubernetes/list) in your Cloud Console and click **Connect**:

![]({{assetRoot}}img/services-packages/deployment-pool/gke-connect.png)

This will give you a `gcloud` command you can paste into your shell and run. You can verify you're connected by running `kubectl cluster-info` - you'll see some information about the Kubernetes cluster you're now connected to.

### 4.1 - Edit configuration files

Now you need to edit the following Kubernetes configuration files with variables that are specific to your deployment:

```
/services/k8s/deployment-pool/deployment.yaml
/services/k8s/analytics-endpoint/deployment.yaml
/services/k8s/analytics-endpoint/service.yaml
/services/k8s/online-services-config.yaml
```

You can use the table below to check which values need to be updated and see examples. The IP address was provided when you applied your Terraform configuration (or navigate into `/services/terraform` and run `terraform output` to view it again), but you can also obtain it from the ([External IP addresses](https://console.cloud.google.com/networking/addresses/list)) page in the Google Cloud Console.

| Name | Description | Example value |
|------|-------------|---------------|
| `{{your_google_project_id}}` | The ID of your Google Cloud project. | `cosmic-abbey-186211` |
| `{{your_analytics_host}}` | The IP address of your analytics service. | `35.235.50.182` |
| `{{your_match_type}}` | A string representing the type of deployment this Pool will look after. | `match` |
| `{{your_assembly_name}}` | The name of the previously uploaded assembly within the SpatialOS project this Pool is running against. | `match_assembly` |
| `{{your_spatialos_project_name}}` | The name of your SpatialOS project. | `alpha_hydrogen_tape_345` |
| `{{your_environment}}` | The environment you set while running Terraform. | `testing` |

### 4.2 - Store your ConfigMaps

Because the Deployment Pool starts deployments, you need to provide a launch configuration file and a snapshot as local files in Kubernetes. You will use Kubernetes ConfigMaps for this purpose so you can mount the files inside a container.

#### 4.2.1 - Launch configuration file

This file should already exist in your SpatialOS project directory. The default name is `default_launch.json`.

Upload it as a ConfigMap in Kubernetes so you can mount it inside a container later.

To do this, run:

```sh
kubectl create configmap launch-config --from-file "{{local_path_to_default_launch_config}}"
```

If your file is not called `default_launch.json`, you need to edit the Kubernetes configuration (`/services/k8s/deployment-pool/deployment.yaml`) before deploying.

#### 4.2.2 - Snapshot

This is a binary file that contains your latest game snapshot. This is usually called something like `default.snapshot`.

Again, upload it as a ConfigMap in Kubernetes so you can mount it inside a container later.

```sh
kubectl create configmap snapshot --from-file "{{local_path_to_default_snapshot_file}}"
```

If your file is not called `default.snapshot`, you need to edit the Kubernetes configuration (`/services/k8s/deployment-pool/deployment.yaml`) before deploying.

### 4.3 - Store your secrets

There are two secrets you need to store on Kubernetes: a SpatialOS refresh token, and an API key for the Analytics Pipeline endpoint.

> A "secret" is the k8s way of storing sensitive information such as passwords and API keys. It means the information isn't stored in any configuration file or - even worse - your source control, but ensures your services still have access to the information they need.

#### 4.3.1 - SpatialOS refresh token

You first need to create a SpatialOS service account. There is a tool in the `online-services` repo to do this for you.

1\. Make sure you're logged in to SpatialOS.

```sh
spatial auth login
```

2\. The tool you need to use is at [`github.com/spatialos/online-services/tree/master/tools/ServiceAccountCLI`](https://github.com/spatialos/online-services/tree/master/tools/ServiceAccountCLI). You can read more about it in the [Platform service-account CLI documentation]({{urlRoot}}/content/workflows/service-account-cli). Navigate to the `/tools/ServiceAccountCLI` directory and run the following command, replacing the `--project_name` parameter with the name of your SpatialOS project (you can change `--service_account_name` to whatever you want, but we've used "online_services_demo" as an example):

```sh
dotnet run -- create --project_name "{{your_spatialos_project_name}}" --service_account_name "online_services_demo" --refresh_token_output_file=service-account.txt --lifetime=0.0:0 --project_write
```

This sets the lifetime to `0.0:0` (in other words, 0 days, 0 hours, 0 minutes), which just means that the refresh token will never expire. You might want to set it to something more appropriate to your needs.

3\. Mount the secret you created into Kubernetes:

```sh
kubectl create secret generic "spatialos-refresh-token" --from-literal="service-account={{your_spatialos_refresh_token}}"
```

<%(Callout type="info" message="Note that you need to enter the actual token, not the path to it.")%>

#### 4.3.2 - Google Cloud project API key

1\. Navigate to [the API credentials overview page for your project in the Cloud Console](https://console.cloud.google.com/apis/credentials) and create a new API key.

2\. Under "API restrictions", select "Restrict key" and then choose "Analytics REST API".

3\. Next, mount the API key into Kubernetes as a secret, replacing `{{your_analytics_api_key}}` with the API key you just created:

```sh
kubectl create secret generic "analytics-api-key" --from-literal="analytics-api-key={{your_analytics_api_key}}"
```

<%(Callout type="info" message="Note that you need to enter the actual analytics API key, not the path to it.")%>

### 4.4 - Deploy to Google Cloud Platform

You can now apply your configuration files to your Kubernetes cluster. Among other things, Kubernetes mounts the snapshot and launch configuration files within the container so the Deployment Pool can read them. By default, these files are called `default_launch.json` and `default.snapshot`. (Edit `/services/k8s/deployment-pool/deployment.yaml` if the files you uploaded have different names.)

To apply your configuration files to your Kubernetes cluster, navigate to `/services/k8s` and run:

```sh
kubectl apply -f online-services-config.yaml
kubectl apply -f online-services-analytics-config.yaml
kubectl apply -Rf analytics-endpoint/
kubectl apply -Rf deployment-pool/
```

You can then check your [Kubernetes Workloads page](https://console.cloud.google.com/kubernetes/workload) and watch as your deployment pool and analytics deployments go green. Congratulations - you've deployed the Deployment Pool together with the Analytics Pipeline successfully!

### Next steps

Check out-of-the-box analytics events from the Deployment Pool [in BigQuery](https://console.cloud.google.com/bigquery) using SQL:

```
SELECT *
FROM events.events_gcs_external
WHERE eventSource = 'deployment_pool'
ORDER BY eventTimestamp
DESC LIMIT 100
;
```

<br/>------------<br/>
