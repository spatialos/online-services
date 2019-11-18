# Gateway (including matchmaking): deploy
<%(TOC)%>

This section shows you how to deploy the Gateway, together with PlayFab Auth and the Analytics Pipeline. You need PlayFab Auth in order to run through an example scenario in [the usage section]({{urlRoot}}/content/services-packages/gateway/usage), and you need the Analytics Pipeline to capture analytics events originating from the Gateway.

## Prerequisites

* Set up everything listed on the [Setup]({{urlRoot}}/content/get-started/setup) page.
* If you don't already have one, sign up for a [PlayFab](https://playfab.com/) account (it's free).

## Step 1 - Create your infrastructure

1\. Ensure your local `gcloud` tool is correctly authenticated with Google Cloud. To do this, run:
    
```sh
gcloud auth application-default login
```

2\. Ensure [the required APIs for your Google project are enabled](https://console.cloud.google.com/flows/enableapi?apiid=serviceusage.googleapis.com,servicemanagement.googleapis.com,servicecontrol.googleapis.com,endpoints.googleapis.com,container.googleapis.com,cloudresourcemanager.googleapis.com,iam.googleapis.com,cloudfunctions.googleapis.com,dataflow.googleapis.com,redis.googleapis.com). When successfully enabled, the response will look like: `Undefined parameter - API_NAMES have been enabled.`.  

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

4\. Run `terraform init`, followed by `terraform apply`. Submit `yes` when prompted.

<%(#Expandable title="Errors with Terraform?")%>If you ran into any errors while applying your Terraform files, first try waiting a few minutes and re-running `terraform apply` followed by `yes` when prompted.<br/><br/>If this does not solve your issue(s), inspect the printed error logs to resolve.<%(/Expandable)%>

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

3\. You need to build the images for each service you want to deploy: `gateway`, `gateway-internal`, `party`, `playfab-auth`, `sample-matcher` and  `analytics-endpoint`. Build the images like this, replacing `{{your_google_project_id}}` with the name of your Google Cloud project:
```sh
docker build --file ./gateway/Dockerfile --tag "gcr.io/{{your_google_project_id}}/gateway" --build-arg CONFIG=Debug ..
docker build --file ./gateway-internal/Dockerfile --tag "gcr.io/{{your_google_project_id}}/gateway-internal" --build-arg CONFIG=Debug ..
docker build --file ./party/Dockerfile --tag "gcr.io/{{your_Google_project_id}}/party" --build-arg CONFIG=Debug ..
docker build --file ./playfab-auth/Dockerfile --tag "gcr.io/{{your_google_project_id}}/playfab-auth" --build-arg CONFIG=Debug ..
docker build --file ./sample-matcher/Dockerfile --tag "gcr.io/{{your_google_project_id}}/sample-matcher" --build-arg CONFIG=Debug ..
docker build --file ./analytics-endpoint/Dockerfile --tag "gcr.io/{{your_google_project_id}}/analytics-endpoint" --build-arg CONFIG=Debug ..
```
<%(#Expandable title="What's happening here?")%>>
<ul>
<li>The `--file` flag tells Docker which Dockerfile to use. A Dockerfile is like a recipe for cooking a container image. We're not going to dive into the contents of Dockerfiles in this guide, but you can read more about them in the [Docker documentation](https://docs.docker.com/engine/reference/builder/) if you're interested.</li>
<li>The `--tag` flag is used to name the image. You should give the container image a name following a particular template so it interfaces correctly with Google Container Registry (GCR). The template it follows is `REGISTRY_NAME_OR_URL/OWNER/IMAGE:TAG`. You can optionally add a **:TAG** at the end, which is usually used to denote the version number of the container image.
<ul>
<li>If you don't provide a `REGISTRY_NAME_OR_URL`, `docker push` and `docker pull` will assume you mean to interact with DockerHub, the open public registry.</li>
<li>If you don't provide a `TAG`, `latest` is automatically used.</li>
</ul>
<li>The `--build-arg` provides variables to the Dockerfile. In this case, you're instructing `dotnet` to do a Debug rather than Release build.</li>
<li>The `..` path at the end tells Docker which directory to use as the build context. You use your services root (`/services`) so that the builder can access our C# service sources.</li>
</ul>
<%(/Expandable)%>

4\. Once you've built all the images, you can push them up to the cloud:
```sh
docker push "gcr.io/{{your_google_project_id}}/gateway"
docker push "gcr.io/{{your_google_project_id}}/gateway-internal"
docker push "gcr.io/{{your_google_project_id}}/party"
docker push "gcr.io/{{your_google_project_id}}/playfab-auth"
docker push "gcr.io/{{your_google_project_id}}/sample-matcher"
docker push "gcr.io/{{your_google_project_id}}/analytics-endpoint"
```

Have a look at your [container registry on the Google Cloud Console](https://console.cloud.google.com/gcr) - you should see your built images there.

## Step 3 - Set up Kubernetes

Kubernetes (or “k8s”) is configured using a tool called `kubectl`. Make sure you [have it installed]({{urlRoot}}/content/get-started/setup#third-party-tools).

Before you do anything else, you need to connect to your Google Kubernetes Engine (GKE) cluster. The easiest way to do this is to go to the [GKE page](https://console.cloud.google.com/kubernetes/list) in your Cloud Console and click **Connect**:

![]({{assetRoot}}img/services-packages/gateway/gke-connect.png)

This gives you a `gcloud` command you can paste into your shell and run. You can verify you're connected by running `kubectl cluster-info` - you'll see some information about the Kubernetes cluster you're now connected to.

### 3.1 - Store your secrets

There are three secrets you need to store on Kubernetes: a SpatialOS refresh token, a PlayFab secret key, and an API key for the Analytics Pipeline endpoint.

> A "secret" is the k8s way of storing sensitive information such as passwords and API keys. It means the information isn't stored in any configuration file or - even worse - your source control, but ensures your services still have access to the information they need.

#### 3.1.1 - PlayFab secret key

1\. Create a new secret key on PlayFab. You'll find this on the dashboard by going to **Settings** > **Secret Keys**. Give it a sensible name so you can revoke it later if you need to.

2\. Run the following command, replacing `{{your-playfab-secret}}` with the secret you just created (it's a mix of numbers and capital letters):

    ```sh
    kubectl create secret generic "playfab-secret-key" --from-literal="playfab-secret={{your-playfab-secret}}"
    ```

You should see:

```sh
secret/playfab-secret-key created
```

Great - your secret's on Kubernetes now, meaning that you can refer to it from your configuration files.

#### 3.1.2 - SpatialOS refresh token

You first need to create a SpatialOS service account. There is a tool in the `online-services` repo to do this for you.

1\. Make sure you're logged in to SpatialOS.

    ```bash
    spatial auth login
    ```

2\. The tool you need to use is at [`github.com/spatialos/online-services/tree/master/tools/ServiceAccountCLI`](https://github.com/spatialos/online-services/tree/master/tools/ServiceAccountCLI). You can read more about it in the [Platform service-account CLI documentation]({{urlRoot}}/content/workflows/service-account-cli). Navigate to the `/tools/ServiceAccountCLI` directory and run the following command, replacing the `--project_name` parameter with the name of your SpatialOS project (you can change `--service_account_name` to whatever you want, but we've used "online_services_demo" as an example):

    ```bash
    dotnet run -- create --project_name "{{your_spatialos_project_name}}" --service_account_name "online_services_demo" --refresh_token_output_file=service-account.txt --lifetime=0.0:0 --project_write
    ```

    This sets the lifetime to `0.0:0` (in other words, 0 days, 0 hours, 0 minutes), which just means that the refresh token will never expire. You might want to set something more appropriate to your needs.

3\. Mount the secret you created into Kubernetes:

    ```sh
    kubectl create secret generic "spatialos-refresh-token" --from-literal="service-account={{your_spatialos_refresh_token}}"
    ```
<%(Callout type="info" message="Note that you need to enter the actual token, not the path to it.")%>

#### 3.1.3 - Google Cloud project API key

1\. Navigate to [the API credentials overview page for your project in the Cloud Console](https://console.cloud.google.com/apis/credentials) and create a new API key.

2\. Under “API restrictions”, select “Restrict key” and then choose ”Analytics REST API”.

3\. Mount the secret you created into Kubernetes, replacing `{{your_analytics_api_key}}` with the API key you just created:

```sh
kubectl create secret generic "analytics-api-key" --from-literal="analytics-api-key={{your_analytics_api_key}}"
```

<%(Callout type="info" message="Note that you need to enter the actual analytics API key, not the path to it.")%>

### 3.2 - Edit configuration files

Now you need to edit the rest of the Kubernetes configuration files with variables that are specific to your deployment, such as your Google Project ID and the external IP addresses of our services.

This part's a little tedious, but you'll only need to do it once. In the various YAML files in the `k8s` directory (except for `k8s/deployment-pool`, refer to its [usage overview]({{urlRoot}}/content/services-packages/deployment-pool/deploy) for more information on how to deploy this one), fill in anything `{{in_double_curly_brackets}}`. You can use the table below to work out what values go where. The IP addresses were provided when you applied your Terraform configuration (or navigate into `/services/terraform` and run `terraform output` to view them again), but you can also obtain them from the ([External IP addresses](https://console.cloud.google.com/networking/addresses/list)) page in the Google Cloud Console.

| Name | Description | Example value |
|------|-------------|---------------|
| `{{your_google_project_id}}` | The ID of your Google Cloud project. | `rhyming-pony-24680` |
| `{{your_spatialos_project_name}}` | The name of your SpatialOS project. | `alpha_hydrogen_tape_345` |
| `{{your_playfab_title_id}}` | Your alphanumeric PlayFab Title ID. | `123A89` |
| `{{your_redis_host}}` | The IP address of your Memorystore. | `10.1.2.3` |
| `{{your_gateway_host}}` | The IP address of your Gateway service. | `123.4.5.6` |
| `{{your_party_host}}` | The IP address of your Party service. | `123.7.8.9` |
| `{{your_playfab_auth_host}}` | The IP address of your Playfab Auth service. | `123.10.11.12` |
| `{{your_analytics_host}}` | The IP address of your Analytics Pipeline service. | `35.235.50.182` |

You can use `git grep "{{.*}}"` to help find which files need editing.

> Kubernetes doesn't provide any templating tools out of the box, so we haven't used any here. However, if you want, you can use a templating system, such as Jinja2, or simply find and replace with `sed`, to do this step more easily.

There are many different types of configuration in Kubernetes; here, you’re using  [`ConfigMap`](https://cloud.google.com/kubernetes-engine/docs/concepts/configmap), [`Deployment`](https://cloud.google.com/kubernetes-engine/docs/concepts/deployment) and [`Service`](https://cloud.google.com/kubernetes-engine/docs/concepts/service).

We won’t go into detail about these right now. To summarize, ConfigMaps hold non-sensitive configuration data, Deployments dictate what runs on a cluster, and Services dictate if and how they are exposed. You'll notice that `sample-matcher` doesn't have a Service configuration - this is because it doesn't expose any ports, being a long-running process rather than an actual web service.

Within the Google Cloud deployments, you define which containers to run in a Pod. Your public services have an additional container, `esp` - this is a Google-provided proxy which you use for service discovery and HTTP transcoding.

Once you're done, you can run `git diff` to view your changes and check the values you entered. Any typos made here may cause issues later on, so it's worth spending a few moments to make sure your changes look good.

### 3.3 - Deploy to Google Cloud Platform

Navigate to the `k8s` directory and run:

```bash
kubectl apply -f online-services-config.yaml
kubectl apply -f online-services-analytics-config.yaml
kubectl apply -Rf gateway/
kubectl apply -Rf gateway-internal/
kubectl apply -Rf party/
kubectl apply -Rf playfab-auth/
kubectl apply -Rf sample-matcher/
kubectl apply -Rf analytics-endpoint/
```

These commands recursively look through every file in the directories, generate configuration from them, and then push them to the cluster. You can then check your [Kubernetes Workloads page](https://console.cloud.google.com/kubernetes/workload) and watch as everything goes green. Congratulations - you've deployed successfully!
