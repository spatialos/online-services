# Cloud Endpoint

This part covers the creation of an endpoint to forward analytics data to, which acts as the start of the analytics pipeline.

1. [Initiating, Verifying & Deploying the Analytics Endpoint](#1---initiating-verifying--deploying-the-analytics-endpoint)
2. [How to Use the Endpoint](#2---how-to-use-the-endpoint)
3. [GKE Debug & Cleanup](#3---gke-debug--cleanup)

## (1) - Initiating, Verifying & Deploying the Analytics Endpoint

### (1.1) - Triggering the Server Code Directly

We will start by calling our custom server code directly via the command line, which will start a local running execution of our endpoint.

_Note: The below are UNIX based commands, if you run Windows best skip this step & go straight to [(1.2)](#12---containerizing-the-analytics-endpoint)._

```bash
# Create a Python 3 virtual environment:
python3 -m venv venv-endpoint

# Activate virtual environment:
source venv-endpoint/bin/activate

# Upgrade Python's package manager pip:
pip install --upgrade pip

# Install dependencies with pip:
pip install -r ../../services/python/analytics-pipeline/src/requirements/endpoint.txt

# Set required environment variables, fill in []:
export GCP=[your project id]
export BUCKET_NAME=[your project id]-analytics
export SECRET_JSON=[local JSON key path writer]
export SECRET_P12=[local p12 key path writer]
export EMAIL=analytics-gcs-writer@[your project id].iam.gserviceaccount.com

# Trigger script!
python ../../services/python/analytics-pipeline/src/endpoint/main.py
# Press Cntrl + C in order to halt execution of the endpoint.

# Exit virtual environment:
# deactivate
```

In a different terminal window, submit the following 2 `curl` POST requests in order to verify the endpoint is working as expected:

```bash
# Verify v1/event method is working:
curl --request POST \
  --header "content-type:application/json" \
  --data "{\"eventSource\":\"client\",\"eventClass\":\"test\",\"eventType\":\"endpoint_main.py\",\"eventTimestamp\":1562599755,\"eventIndex\":6,\"sessionId\":\"f58179a375290599dde17f7c6d546d78\",\"buildVersion\":\"2.0.13\",\"eventEnvironment\":\"testing\",\"eventAttributes\":{\"playerId\": 12345678}}" \
  "http://0.0.0.0:8080/v1/event?&analytics_environment=testing&event_category=cold&session_id=f58179a375290599dde17f7c6d546d78"

# Verify v1/file method is working:
curl --request POST \
  --header 'content-type:application/json' \
  --data "{\"content_type\":\"text/plain\", \"md5_digest\": \"XKvMhvwrORVuxdX54FQEdg==\"}" \
  "http://0.0.0.0:8080/v1/file?&analytics_environment=testing&event_category=crashdump-worker&file_parent=parent&file_child=child"  
```

If both requests returned proper JSON, without any error messages, the endpoint is working as expected! :tada:

### (1.2) - Containerizing the Analytics Endpoint

Next we are going to create an image which contains everything required in order to run our Analytics Endpoint using [Docker](https://www.docker.com/). We will then:

1. Verify the image by executing the image in a local container (a container is a running instance of an image).
2. Once we have verified this is the case, we will run our container alongside a second public container provided by Google which handles everything related to [Cloud Endpoints](https://cloud.google.com/endpoints/) in a local pod (a pod is a group of containers running together), mimicking the situation it will be in once deployed on [Google Kubernetes Engine](https://cloud.google.com/kubernetes-engine/) (GKE - Google's fully managed Kubernetes solution).
3. After we have verified our local pod is running as expected as well, we will push the image to a remote location, in this case [Google Container Registry (GCR)](https://cloud.google.com/container-registry/), which stages the image to be deployed as containers on your Kubernetes cluster.

#### (1.2.1) - Verifying the Analytics Endpoint Image

```bash
# Build image:
docker build -f ../../services/docker/analytics-endpoint/Dockerfile -t "gcr.io/[your project id]/analytics-endpoint" ../../services

# Execute image as container & step inside it to explore it:
docker run -it \
  --env GCP=[your project id] \ # Sets an environment variable
  --env BUCKET_NAME=[your project id]-analytics \
  --env SECRET_JSON=/secrets/json/analytics-gcs-writer.json \
  --env SECRET_P12=/secrets/p12/analytics-gcs-writer.p12 \
  --env EMAIL=analytics-gcs-writer@[your project id].iam.gserviceaccount.com \
  -v [local JSON key path writer]:/secrets/json/analytics-gcs-writer.json \ # Mount volume from_local_path:to_path_in_container
  -v [local p12 key path writer]:/secrets/p12/analytics-gcs-writer.p12 \
  --entrypoint bash \ # Override the default entrypoint of container
  gcr.io/[your project id]/analytics-endpoint:latest # Image you want to execute as a container

# Tip - Type & submit 'exit' to stop the container
```

Now let's verify the container is working as expected, by running it locally:

```bash
# Execute image as container:
docker run \
  --env GCP=[your project id] \
  --env BUCKET_NAME=[your project id]-analytics \
  --env SECRET_JSON=/secrets/json/analytics-gcs-writer.json \
  --env SECRET_P12=/secrets/p12/analytics-gcs-writer.p12 \
  --env EMAIL=analytics-gcs-writer@[your project id].iam.gserviceaccount.com \
  -v [local JSON key path writer]:/secrets/json/analytics-gcs-writer.json \
  -v [local p12 key path writer]:/secrets/p12/analytics-gcs-writer.p12 \
  -p 8080:8080 \
  gcr.io/[your project id]/analytics-endpoint:latest
```

As before, in a different terminal window, submit the follow 2 curl POST requests:

```bash
# Verify v1/event method is working:
curl --request POST \
  --header "content-type:application/json" \
  --data "{\"eventSource\":\"client\",\"eventClass\":\"test\",\"eventType\":\"endpoint_docker_run\",\"eventTimestamp\":1562599755,\"eventIndex\":6,\"sessionId\":\"f58179a375290599dde17f7c6d546d78\",\"buildVersion\":\"2.0.13\",\"eventEnvironment\":\"testing\",\"eventAttributes\":{\"playerId\": 12345678}}" \
  "http://0.0.0.0:8080/v1/event?&analytics_environment=testing&event_category=cold&session_id=f58179a375290599dde17f7c6d546d78"

# Verify v1/file method is working:
curl --request POST \
  --header 'content-type:application/json' \
  --data "{\"content_type\":\"text/plain\", \"md5_digest\": \"XKvMhvwrORVuxdX54FQEdg==\"}" \
  "http://0.0.0.0:8080/v1/file?&analytics_environment=testing&event_category=crashdump-worker&file_parent=parent&file_child=child"

# To stop the running container, press Cntrl + C, or:
docker ps # Copy the [container id]
docker kill [container id]
```

If both requests returned proper JSON again without any errors, we have verified our Analytics Endpoint image is working :boom:!

#### (1.2.2) - Verifying the Analytics Endpoint with ESP

When deploying the Analytics Endpoint on GKE, we will do this by deploying a pod (or multiple copies of a pod) that contains two containers:

- Our Analytics Endpoint with custom server code.
- A public ESP container provided by Google which runs everything related to [Cloud Endpoints](https://cloud.google.com/endpoints/).

In order to mimic this situation locally, we will use [docker-compose](https://docs.docker.com/compose/).

First you need to [get an API key for your GCP](https://console.cloud.google.com/apis/credentials), which you need to pass via the **key** parameter in the url of your POST request: **[your gcp api key]**. This is something we configured the ESP container to require (basic auth), before forwarding the request onto our Analytics Endpoint. Note that it's currently [not possible to provision an API key programmatically](https://issuetracker.google.com/issues/76227920) & that **it takes some time before API keys become fully functional, to be safe wait at least 10 minutes** before attempting the below POST requests.

```bash
# First set a few environment variables:
export GCP=logical-flame-194710
export SECRET_JSON=[local JSON key path writer]
export SECRET_P12=[local p12 key path writer]
export SECRET_JSON_ESP=[local JSON key path endpoint]
export IMAGE=analytics-endpoint

# Start a local pod containing both containers:
docker-compose -f ../../services/docker/docker_compose_local_analytics.yml up

# Verify v1/event method is working:
curl --request POST \
  --header "content-type:application/json" \
  --data "{\"eventSource\":\"client\",\"eventClass\":\"test\",\"eventType\":\"endpoint_docker_compose\",\"eventTimestamp\":1562599755,\"eventIndex\":6,\"sessionId\":\"f58179a375290599dde17f7c6d546d78\",\"buildVersion\":\"2.0.13\",\"eventEnvironment\":\"testing\",\"eventAttributes\":{\"playerId\": 12345678}}" \
  "http://0.0.0.0:8080/v1/event?key=[your gcp api key]&analytics_environment=testing&event_category=cold&session_id=f58179a375290599dde17f7c6d546d78"

# Verify v1/file method is working:
curl --request POST \
  --header 'content-type:application/json' \
  --data "{\"content_type\":\"text/plain\", \"md5_digest\": \"XKvMhvwrORVuxdX54FQEdg==\"}" \
  "http://0.0.0.0:8080/v1/file?key=[your gcp api key]&analytics_environment=testing&event_category=crashdump-worker&file_parent=parent&file_child=child"

# To stop execution of our local pod press Cntrl + C, or:
docker ps # Copy [container id 1] & [container id 2]
docker kill [container id 1] [container id 2]
```

In case the requests were successful again, we can now proceed to push our Analytics Endpoint image to GCR! :clap:

#### (1.2.3) - Pushing the Analytics Endpoint Image to GCR

The following commands will take your local Analytics Endpoint Docker image and push it to [Google Container Registry](https://cloud.google.com/container-registry/).

```bash
# Make sure you are in the right project:
gcloud config set project [your project id]

# Upload image to Google Container Registry (GCR):
docker push gcr.io/[your project id]/analytics-endpoint:latest

# Verify your image is uploaded:
gcloud container images list
```

Alternatively, you can verify your image is uploaded [in the Cloud Console](https://console.cloud.google.com/gcr/images/).

### (1.3) - Deploying Analytics Endpoint Container onto GKE with Cloud Endpoints

At this point we have a working image hosted in GCR, which GKE can pull from. We will now deploy our Analytics Endpoint on your Kubernetes cluster. You can check out what your **[your k8s cluster name]** & **[your k8s cluster location]** are [in the Cloud Console](https://console.cloud.google.com/kubernetes/list).

```bash
# Make sure you have the credentials to talk to the right cluster:
gcloud container clusters get-credentials [your k8s cluster name] --zone [your k8s cluster location]

# Or if you already do - that you're configured to talk to the right cluster:
kubectl config get-contexts # Copy the correct [your k8s context name]
kubectl config use-context [your k8s context name]
```

We now first need to make a few edits to our Kubernetes YAML files:

- Update the [deployment.yaml](../../services/k8s/analytics-endpoint/deployment.yaml) file with **[your project id]**.
- Update the [service.yaml](../../services/k8s/analytics-endpoint/service.yaml) file with **[your Analytics IP address]**. You can check out what this value is by navigating into [terraform/](../../services/terraform) & running `terraform output` (look for **analytics_host**).

**Afterwards** deploy the deployment & service to GKE:

```bash
kubectl apply -f ../../services/k8s/analytics-endpoint
```

```bash
# Verify v1/event method is working:
curl --request POST \
  --header "content-type:application/json" \
  --data "{\"eventSource\":\"client\",\"eventClass\":\"test\",\"eventType\":\"endpoint_k8s\",\"eventTimestamp\":1562599755,\"eventIndex\":6,\"sessionId\":\"f58179a375290599dde17f7c6d546d78\",\"buildVersion\":\"2.0.13\",\"eventEnvironment\":\"testing\",\"eventAttributes\":{\"playerId\": 12345678}}" \
  "http://analytics.endpoints.[your project id].cloud.goog:80/v1/event?key=[your gcp api key]&analytics_environment=testing&event_category=cold&session_id=f58179a375290599dde17f7c6d546d78"

# Verify v1/file method is working:
curl --request POST \
  --header 'content-type:application/json' \
  --data "{\"content_type\":\"text/plain\", \"md5_digest\": \"XKvMhvwrORVuxdX54FQEdg==\"}" \
  "http://analytics.endpoints.[your project id].cloud.goog:80/v1/file?key=[your gcp api key]&analytics_environment=testing&event_category=crashdump-worker&file_parent=parent&file_child=child"
```

If both requests succeeded, this means you have now deployed your Analytics Endpoint! :confetti_ball:

## (2) - How to Use the Endpoint

### (2.1) - `/v1/event`

This method enables you to store analytics events in your GCS analytics bucket. The method:

- Accepts JSON dicts (one for each event), either standalone or batched up in lists (recommended).
- Augments JSON events with **batchId**, **eventId**, **receivedTimestamp** & **analyticsEnvironment**.
- Writes received JSON data as newline delimited JSON event files in GCS, which facilitates easy ingestion into BigQuery.
- Determines the files' location in GCS through endpoint URL parameters. These in turn determine whether the events are ingested into native BigQuery storage (vs. GCS as external storage) by default.
- Also accepts non-JSON data.

#### (2.1.1) - URL Parameters

The URL takes 6 parameters:

| Parameter               | Class    | Description |
|-------------------------|----------|-------------|
| `key`                   | Required | Must be tied to your GCP ([info](https://cloud.google.com/endpoints/docs/openapi/get-started-kubernetes#create_an_api_key_and_set_an_environment_variable)). |
| `analytics_environment` | Should   | Should be set, must be one of {**testing**, **development** (default), **staging**, **production**, **live**}. |
| `event_category`        | Should   | Should be set, otherwise defaults to **cold**. |
| `event_ds`              | Optional | Generally not set, defaults to the current UTC date in **YYYY-MM-DD**. |
| `event_time`            | Optional | Generally not set, defaults to the current UTC time part, otherwise must be one of {**0-8**, **8-16**, **16-24**}. |
| `session_id`            | Should   | Should be set, otherwise defaults to **session-id-not-available**. |

These parameters (except for `key`) influence where the data ends up in the GCS bucket:

> gs://gcp-analytics-pipeline-events/data\_type={data\_type}/analytics\_environment={analytics\_environment}/event\_category={event\_category}/event\_ds={event\_ds}/event\_time={event\_time}/{session\_id}/{ts\_fmt}\-{rand_int}

Note that **{data_type}** is determined automatically and can either be **json** (when valid JSON is POST'ed) or **unknown** (otherwise). The fields **{ts_fmt}** (a human-readable timestamp) & **{rand_int}** (a random integer to avoid collisions) are automatically set by the endpoint as well.

Note that the **event_category** parameter is particularly **important**:

- When set to **function** all data contained in the POST request will be **ingested into native BigQuery storage** using [the analytics Cloud Function (`function-gcs-to-bq-.*`)](https://console.cloud.google.com/functions/list) we created when we deployed [the analytics module with Terraform]((https://github.com/improbable/online-services/tree/master/services/terraform)). More information about this later in [the third part of the Analytics Pipeline documentation](./3-bigquery-cloud-function.md).
- When set to **anything else** all data contained in the POST request will **arrive in GCS**, but will **not by default be ingested into native BigQuery storage**. This data can however still be accessed with BigQuery by using GCS as an external data source. More information about this later in [the second part of the Analytics Pipeline documentation](./2-bigquery-gcs-external.md).

Note that **function** is a completely arbitrary string, but we have established [GCS notifications to trigger Pub/Sub notifications to the Pub/Sub Topic that feeds our analytics Cloud Function](../../services/terraform/module-analytics/pubsub.tf) whenever files are created on this particular GCS prefix. In this case these notifications invoke our analytics Cloud Function which ingests these files into native BigQuery storage.

Over-time we can imagine developers extending this setup in new ways: perhaps crashdumps are written into **crashdump** (either via `v1/event` or `v1/file` depending on size) which will trigger a different Cloud Function with the appropriate logic to parse it and write relevant information into BigQuery, or **frames_per_second** will be used for high volume frames-per-second events that are subsequently aggregated with a Dataflow (Stream / Batch) script _before_ being written into BigQuery.

#### (2.1.2) - The JSON Event Schema

Each analytics event, which is a JSON dictionary, should adhere to the following JSON schema:

| Key                | Type    | Description |
|--------------------|---------|-------------|
| `eventEnvironment` | string  | One of {testing, development, staging, production, live}. |
| `eventIndex`       | integer | Increments with one with each event per sessionId, allows spotting missing data. |
| `eventSource`      | string  | Source of the event (e.g. client/server ~ worker type). |
| `eventClass`       | string  | A higher order mnemonic classification of events (e.g. session). |
| `eventType`        | string  | A mnemonic event identifier (e.g. session_start). |
| `sessionId`        | string  | The session_id, which is unique per worker (e.g. client/server) session. |
| `buildVersion`     | string  | Version of the build, should naturally sort from oldest to latest. |
| `eventTimestamp`   | float   | The timestamp of the event, in unix time. |
| `eventAttributes`  | dict    | Anything else relating to this particular event will be captured in this attribute as a nested JSON dictionary. |

**Keys should always be camelCase**, whereas values snake_case whenever appropriate. The idea is that all **root keys of the dictionary** are **always present for any event**. Anything custom to a particular event should be nested within eventAttributes. If there is nothing to nest it should be an empty dict (but still present).

In case a server-side event is triggered around a player (vs. AI), always make sure the playerId (or characterId) is captured within eventAttributes. Else you will have no way of knowing which player the event belonged to. For client-side events, as long as there is at least one event which pairs up the playerId with the client's sessionId (e.g. `login`), we can always backtrack which other client-side events belonged to a specific player.

Finally, note that playerId is not a root field of our events, because it will not always be present for any event (e.g. AI induced events, client-side events pre-login, etc.).

#### (2.1.3) - Instrumentation Tips

Your logic should include:

- Augmenting events automatically with all required root fields of an event, only requiring custom event attributes to be passed in (which all go into `eventAttributes`).
- Batching events up before emitting them (every X seconds or Y events [~payload size], whichever comes first).
- Making a POST request to a configurable endpoint (e.g. which host, URL parameters & headers).
    + Ideally, the logic can handle batching + custom URL parameters per `eventClass`/`eventType`, so for instance all `frames_per_second` events will be POST'ed in batches to `..&event_category=frames_per_second..`, staging them for aggregation before being ingested into BigQuery, whereas less frequent events can go to `..&event_category=function..`, etc.
- Allowing the configuration of retries & have fallback mechanisms (e.g. first try production endpoint, then staging endpoint, then testing endpoint).
- Capping the bandwidth provided to emitting events, as to never degrade game performance too much.
- In case hook is client-side: local caching of events in case connection is offline.

### (2.2) - `/v1/file`

This method allows you to write large files straight into GCS, without having to route the entire file contents via the Analytics Endpoint (which might otherwise overload it). The process entails requesting a signed URL which authorizes your server/client to write a specific file (based on its base64 encoded md5 hash) to a specific location in GCS within 30 minutes.

Assuming you have a crashdump file called **worker-crashdump-test.gz**:

```bash
# Get the base64 encoded md5 hash:
openssl md5 -binary worker-crashdump-test.gz | base64
# > XKvMhvwrORVuxdX54FQEdg==

# Send only this hash to endpoint, set URL parameters to define how the file ends up in GCS:
curl --request POST \
  --header 'content-type:application/json' \
  --data "{\"content_type\":\"text/plain\", \"md5_digest\": \"XKvMhvwrORVuxdX54FQEdg==\"}" \
  "http://analytics.endpoints.[your project id].cloud.goog:80/v1/file?key=[your gcp api key]&analytics_environment=testing&event_category=crashdump-worker&file_parent=parent&file_child=child"

# Grab the signed URL & headers from the returned JSON dictionary (if successful) & write file directly into GCS within 30 minutes:
curl \
  -H 'Content-Type: text/plain' \
  -H 'Content-MD5: XKvMhvwrORVuxdX54FQEdg==' \
  -X PUT "https://storage.googleapis.com/gcp-analytics-pipeline-events/data_type=file/analytics_environment=testing/event_category=crashdump-worker/event_ds=2019-06-18/event_time=8-16/parent/child-451684?GoogleAccessId=analytics-gcs-writer%40[your project id].iam.gserviceaccount.com&Expires=1560859391&Signature=tO0bvOzgbF%2F%2FYt%2F%2BHr5L9oH1Y9yQIYMBFIuFyb36L3UhSzalq3%2FRYmto2lguceSoHEtknZQaeI1zDqRwEqfGkPTDGMY9bE1wNR9aT%2F8aAitC0czl6cOPVyJ%2FE1%2B7riEBHXcJyQQSsDMUeJWWT50OKWX4yM961kfJK7c7mv0bvwJPint7Eo5iPTyR9ax57gb4bgSgtFV5MM5c%2FvCIH7%2BuUAiXSbW9CWsA56UJRNf%2BB0YplRtB12VlxWyQlZKpHFrU5EoLQ3vO3YXsQidkjm1it%2BCl1uQptvX%2BZCI7eleEiZANpVX46%2B0MFSXi%2FidMHQSVEF96iGTaFvwzpoiT%2Bj%2F42g%3D%3D" \
  --data-binary '@worker-crashdump-test.gz'
```

## (3) - GKE Debug & Cleanup

The following commands can help you check what is happening with your pods in GKE & potentially debug any issues. Note a pod is generally a group of containers running together. In our case, each pod contains two containers: one running our custom server code & one public Google container that runs everything related to Cloud Endpoints. We are running 3 replicas of our pod together in a single deployment. This means we have 2 x 3 = 6 containers running in our deployment.

```bash
# Show me all deployments:
kubectl get deployments

# Show me all pods:
kubectl get pods # Copy the [pod id]

# View details of pods:
kubectl describe pod [pod id]

# Show logs of a specific container:
kubectl logs [pod id] [container name]

# Step inside running container:
kubectl exec [pod id] -c [container name] -it bash

# Where [container name] = analytics-deployment-server or analytics-deployment-endpoint
```

In case you want remove your workloads (service & deployment) from GKE, you can run the following commands:

```bash
# Obtain list of all your deployments:
kubectl get deployments # Copy the [deployment name]

# Delete your deployment:
kubectl delete deployment [deployment name]

# Obtain list of all your services:
kubectl get services # Copy the [service name]

# Delete your service:
kubectl delete service [service name]
```

---

Next up: [(2) - Using GCS as an external data source through BigQuery](./2-bigquery-gcs-external.md)
