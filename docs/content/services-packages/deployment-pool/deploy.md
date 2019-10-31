# Deployment Pool: deploy
<%(TOC)%>

## Prerequisites

0. Set up everything listed on the [Setup]({{urlRoot}}/content/get-started/setup) page.

## Configuration

The Deployment Pool requires information about the deployments it is going to start. Most of this information is passed as flags to the Deployment Pool when it starts up. The full list of flags is as follows:

| Parameter           | Type       | Purpose |
|---------------------|------------|---------|
| `--deployment-prefix` | `optional` | Deployments created by the Pool are allocated a random name. Use this to further add a custom prefix to all these deployments, e.g. `pool-`. |
| `--minimum-ready-deployments` | `optional` | The number of "ready-to-go" deployments to maintain. Defaults to 3. |
| `--match-type` | `required` | A string representing the type of deployment this Pool will look after. For example, "fps", "session", "dungeon0". |
| `--project` | `required` | The SpatialOS project to start deployments in. The Deployment Pool must have write access to this project to start deployments. |
| `--snapshot` | `required` | The path to the deployment snapshot to start any deployments with. |
| `--launch-config` | `required` | The path to the launch configuration JSON file to start any deployments with. |
| `--assembly-name` | `required` | The name of the previously uploaded assembly within the SpatialOS project this Pool is running against. |
| `--analytics.endpoint` | `optional` | Should be "http://analytics.endpoints.{{your_google_project_id}}.cloud.goog:80/v1/event" |
| `--analytics.allow-insecure-endpoint` | `optional` | If using an HTTP endpoint (which you are by default), must be passed. |
| `--analytics.config-file-path` | `optional` | Path to the analytics event configuration file, by default "/config/online-services-analytics-config.yaml". |
| `--analytics.gcp-key-path` | `optional` | Path to your Analytics REST API key, by default "/secrets/analytics-api-key". |
| `--analytics.environment` | `optional` | Environment of the endpoint you are using, e.g. one of: {testing, staging, production, ...}. |

The final five flags are to configure the out-of-the-box instrumentation that come with the Deployment Pool. If any of these are missing the Deployment Pool will still function, though no analytics events will be captured. Finally, the Deployment Pool requires a `SPATIAL_REFRESH_TOKEN` environment variable containing a SpatialOS refresh token, which provides authentication for it to be able to use the SpatialOS Platform.

## Step 1 - Create your infrastructure

Ensure your local `gcloud` tool is correctly authenticated with Google Cloud. Run:

```sh
gcloud auth application-default login
```

0. In your copy of the online-services repo, navigate to `/services/terraform` & create a file called `terraform.tfvars`. In there, set the following variables:

| Variable | Description   |
-----------|---------------|
| `gcloud_project` | Your cloud project ID. Note that this is the ID, not the display name. |
| `gcloud_region` | A region; pick one from [google cloud](https://cloud.google.com/compute/docs/regions-zones/), ensuring you pick a region and not a zone (zones live within regions). |
| `gcloud_zone` | A zone; ensure this zone is within your chosen region. For example, the zone `europe-west1-c` is within region `europe-west1`. |
| `k8s_cluster_name` | A name for your cluster. You can put whatever you like here. |
| `cloud_storage_location` | The location of your Cloud Storage buckets, either `US` or `EU`.|

The contents of your `terraform.tfvars` file should look something like:

```
gcloud_project         = "cosmic-abbey-186211"
gcloud_region          = "europe-west2"
gcloud_zone            = "europe-west2-b"
k8s_cluster_name       = "online-services-testing"
cloud_storage_location = "EU"
```

0. Navigate into `/services/terraform/modules.tf` & comment out all sections except for Analytics. This will cause only the required infrastructure for the Deployment Pool & Analytics (for tracking of the Deployment Pool) to be provisioned.

0. Run `terraform init`, followed by `terraform apply`. Submit `Yes` when prompted.

# Step 2 - Build your service images

You need to use Docker to build your services as containers, then push them up to our Google Cloud project’s container registry. To start, you need to configure Docker to talk to Google.

0. Run the following commands in order:

```sh
gcloud components install docker-credential-gcr
gcloud auth configure-docker
gcloud auth login
```

Now you can build and push the Docker images for your services.

0. Navigate to the directory where the Dockerfiles are kept (`/services/docker`).

0. Build the images like this, replacing `{{your_google_project_id}}` with the name of your Google Cloud project:

```sh
docker build -f ./deployment-pool/Dockerfile -t "gcr.io/{{your_google_project_id}}/deployment-pool" ..
docker build -f ./analytics-endpoint/Dockerfile -t "gcr.io/{{your_google_project_id}}/analytics-endpoint" ..
```
0. Once you’ve built the images, push them up to the cloud:

```sh
docker push "gcr.io/{{your_google_project_id}}/deployment-pool"
docker push "gcr.io/{{your_google_project_id}}/analytics-endpoint"
```

Have a look at your [container registry on the Google Cloud Console](https://console.cloud.google.com/gcr) - you should see your built images there.

## Step 3 - Create secrets

### SpatialOS Refresh Token

You first need to create a SpatialOS service account. There is a tool in the online-services repo to do this for you.

0. First you need to make sure you're logged in to SpatialOS.

```bash
spatial auth login
```

The tool you're using lives at [`github.com/spatialos/online-services/tree/master/tools/ServiceAccountCLI`](https://github.com/spatialos/online-services/tree/master/tools/ServiceAccountCLI). You can read more about it in the [Service account CLI tool documentation]({{urlRoot}}/content/workflows/service-account-cli).

0. Navigate to the `/tools/ServiceAccountCLI` directory and run the following command, replacing the `--project_name` parameter with the name of your SpatialOS project (you can change `--service_account_name` to whatever you want, but we've used "online_services_demo" as an example):

```bash
dotnet run -- create --project_name "{{your_spatialos_project_name}}" --service_account_name "online_services_demo" --refresh_token_output_file=service-account.txt --lifetime=0.0:0 --project_write
```

You've set the lifetime to `0.0:0` here (i.e. 0 days, 0 hours, 0 minutes) - this just means it'll never expire. You might want to set something more appropriate to your needs.

### GCP API Key

0. Navigate to [the API credentials overview page in the Google UI](https://console.cloud.google.com/apis/credentials) of your Google project & create a new API key. After creation, restrict the API key to the **Analytics REST API** & note it down, you will use it later.

## Step 4 - Upload a SpatialOS assembly

To start a deployment, a previously uploaded assembly is required. This can be completed using the SpatialOS CLI from your SpatialOS Project files. (See the CLI documentation in the [Tools: CLI](https://docs.improbable.io/reference/latest/shared/spatialos-cli-introduction) section of the SpatialOS documentation.)

0. Upload your assembly to SpatialOS ahead of time so that the pool can access it when it starts deployments.

```bash
spatial upload {{assembly_name}}
```

The `assembly_name` is the string you will need to pass to the Deployment Pool later: `--assembly-name`.

## Step 5 - Run the Deployment Pool locally

To test the Deployment Pool locally, you need a previously uploaded assembly, a snapshot file for your deployment and a launch configuration file.

0. Set the `SPATIAL_REFRESH_TOKEN` environment variable to the refresh token you created in Step 3. You can do it like this on Windows Command Prompt:

```bat
: Note that you'll need to start a new Command Prompt after running this.
setx SPATIAL_REFRESH_TOKEN "{{your_refresh_token}}"
```

On other platforms:

```bash
export SPATIAL_REFRESH_TOKEN="{{your_refresh_token}}"

# & Use `$SPATIAL_REFRESH_TOKEN` instead of `%SPATIAL_REFRESH_TOKEN%` in the docker command below!
```

_Note that you need to input the actual token, not the path to the token!_

0. Once these are in place, you can start the Deployment Pool using:

```bash
docker run -v {{local_path_to_launch_config}}:/launch-config/default_launch.json -v {{local_path_to_snapshot_file}}:/snapshots/default.snapshot -e SPATIAL_REFRESH_TOKEN=%SPATIAL_REFRESH_TOKEN% gcr.io/{{your_google_project_id}}/deployment-pool --project "{{your_spatialos_project_name}}" --launch-config "/launch-config/default_launch.json" --snapshot "/snapshots/default.snapshot" --assembly-name "{{your_uploaded_assembly_name}}" --minimum-ready-deployments 3
```

The refresh token is passed as an environment variable as it is a secret and shouldn't be passed in plaintext. It is recommended to set the secret up from an external source, for example from a properly secured local file, then use `cat my-spatial-refresh-token` in the command above to avoid storing it in your command history.

## Step 6 - Set up Kubernetes

Kubernetes (or **k8s**) is configured using a tool called `kubectl`. Make sure you [have it installed]({{urlRoot}}/content/get-started/setup#third-party-tools).

Before you do anything else you need to connect to your GKE cluster. The easiest way to do this is to go to the [GKE page](https://console.cloud.google.com/kubernetes/list) on your Cloud Console and click the 'Connect' button:

![]({{assetRoot}}img/quickstart/gke-connect.png)

This will give you a `gcloud` command you can paste into your shell and run. You can verify you're connected by running `kubectl cluster-info` - you'll see some information about the Kubernetes cluster you're now connected to.

### Store your secrets

0. Mount the secrets you created in Step 3 into Kubernetes:

```bash
kubectl create secret generic "spatialos-refresh-token" --from-literal="service-account={{your-spatialos-refresh-token}}"
kubectl create secret generic "analytics-api-key" --from-literal="analytics-api-key={{your-analytics-api-key}}"
```

### Edit configuration files

0. Now you need to edit the following Kubernetes configuration files with variables that are specific to your deployment, such as your Google Project Id and the external IP address of your analytics endpoint:

```
/services/k8s/deployment-pool/deployment.yaml
/services/k8s/analytics-endpoint/deployment.yaml
/services/k8s/analytics-endpoint/service.yaml
/services/k8s/online-services-config.yaml
```

You can use the table below to work out what values go where - the IP address will have been provided to you when you applied your Terraform configuration (or navigate into `/services/terraform` & run `terraform output`), but you can also obtain it from the ([External IP addresses](https://console.cloud.google.com/networking/addresses/list)) page in the Google Cloud Console.

| Name | Description | Example Value |
| ---- | ----------- | ------------- |
| `{{your_google_project_id}}` | The ID of your Google Cloud project | `cosmic-abbey-186211` |
| `{{your_analytics_host}}` | The IP address of your Analytics service | `35.235.50.182` |
| `{{your_match_type}}` | A string representing the type of deployment this Pool will look after. For example, "fps", "session", "dungeon0". |
| `{{your_assembly_name}}` | The name of the previously uploaded assembly within the SpatialOS project this Pool is running against. |
| `{{your_analytics_environment}}` | Environment of the endpoint you are using, e.g. one of: {testing, staging, production, ...}. |

### Create config maps

As the Deployment Pool will be starting deployments, you will need to provide a launch configuration and a snapshot as local files in Kubernetes. You will use Kubernetes config maps for this purpose so the files can be mounted alongside a pod.

#### Launch configuration

This file should already exist in your SpatialOS project directory. The default name is `default_launch.json`.

0. Upload it as a config map in Kubernetes so this file can be mounted later.

```bash
kubectl create configmap launch-config --from-file "{{local_path_to_launch_config}}"
```

If your file is not called "default_launch.json" you may need to edit the Kubernetes configuration before deploying (`/services/k8s/deployment-pool/deployment.yaml`).

#### Snapshot

This is a binary file which contains your latest game snapshot. This is usually called something like `default.snapshot`.

0. Again, upload it as a config map in Kubernetes so this file can be mounted later.

```bash
kubectl create configmap snapshot --from-file "{{local_path_to_snapshot_file}}"
```

If your file is not called "default.snapshot" you may need to edit the Kubernetes configuration before deploying (`/services/k8s/deployment-pool/deployment.yaml`).

### Deploy to Google Cloud Platform

You can now apply your configuration files to your Kubernetes cluster. Kubernetes will mount the Snapshot and Launch Configuration files within the Pod so the Deployment Pool will be able to read them. By default, these files are expected to be called "default_launch.json" and "default.snapshot". Edit `/services/k8s/deployment-pool/deployment.yaml` if the files you uploaded have different names.

0. Navigate to the `/services/k8s` directory and run:

```sh
kubectl apply -f online-services-config.yaml
kubectl apply -f online-services-analytics-config.yaml
kubectl apply -Rf analytics-endpoint/
kubectl apply -Rf deployment-pool/
```

You can then check your [Kubernetes Workloads page](https://console.cloud.google.com/kubernetes/workload) and watch as your deployment pool & analytics deployments go green. Congratulations - you've deployed the Deployment Pool together with the Analytics Pipeline successfully!

<%(Nav hide="next")%>
<%(Nav hide="prev")%>

<br/>------------<br/>
