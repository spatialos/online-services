# Analytics Pipeline: local
<%(TOC)%>

## Prerequisites

* This page assumes you have already [deployed the Analytics Pipeline]({{urlRoot}}/content/services-packages/analytics-pipeline/deploy).
* If you're on Windows, you need to take some additional steps to mount Docker volumes. These steps are in a separate [Docker volumes on Windows]({{urlRoot}}/content/workflows/docker-windows-volumes.md) guide.

## Overview

In this section you will execute the Analytics Pipeline code locally. When deploying the endpoint of the Analytics Pipeline on GKE, you are deploying a Pod (or multiple copies of a Pod) that contains two containers:

- One container with your custom server code.
- A public ESP container provided by Google, which runs everything related to [Cloud Endpoints](https://cloud.google.com/endpoints/).

To mimic deploying a Pod that consists of two containers locally, you will use [`docker-compose`](https://docs.docker.com/compose/), which is already included with Docker.

## Deploy locally

In the beginning of this section you will need to note down a few values. We have labelled these in `{{double_curly_brackets}}`, and will refer back to them afterwards.

1\. Navigate to [the Service accounts overview](https://console.cloud.google.com/iam-admin/serviceaccounts) in the IAM section within the Cloud Console for your Google project, and:

* Create and store a local JSON **and** P12 key from the service account named "Analytics GCS Writer". Note down their local paths: `{{your_local_path_json_key_analytics_gcs_writer}}` and `{{your_local_path_p12_key_analytics_gcs_writer}}`.
* Create and store a local JSON key from the service account named "Analytics Endpoint". Note down its local path: `{{your_local_path_json_key_analytics_endpoint}}`.

2\. Set the following environment variables:

| Environment variable | Value |
|----------------------|-------|
| `GOOGLE_PROJECT_ID` | `{{your_google_project_id}}` |
| `GOOGLE_SECRET_KEY_JSON_ANALYTICS_GCS_WRITER` | `{{your_local_path_json_key_analytics_gcs_writer}}` |
| `GOOGLE_SECRET_KEY_P12_ANALYTICS_GCS_WRITER` | `{{your_local_path_p12_key_analytics_gcs_writer}}` |
| `GOOGLE_SECRET_KEY_JSON_ANALYTICS_ENDPOINT` | `{{your_local_path_json_key_analytics_endpoint}}` |
| `ANALYTICS_ENVIRONMENT` | `{{your_environment}}` |

To do this, on Windows Command Prompt, run:

```bat
setx GOOGLE_PROJECT_ID "{{your_google_project_id}}"
```

Note that you need to start a new Command Prompt window after running this.

On other platforms, run:

```sh
export GOOGLE_PROJECT_ID="{{your_google_project_id}}"
```

3\. From within the root of the `online-services` repo, run the following command:

```sh
docker-compose -f services/docker/docker_compose_local_analytics_pipeline.yml up
```

4\. Find [the API key for your Google project](https://console.cloud.google.com/apis/credentials) that you created in [step 3.1 of the Deploy section]({{urlRoot}}/content/services-packages/analytics-pipeline/deploy#3-1-store-your-secret), which you need to pass via the `key` parameter in the URL of your POST request: `{{your_analytics_api_key}}`.

5\. Replace `{{your_analytics_api_key}}` with your API key in the curl request below and submit it in your terminal:

```sh
curl --request POST --header "Content-Type:application/json" --data "{\"eventSource\":\"client\",\"eventClass\":\"docs\",\"eventType\":\"endpoint_docker_compose\",\"eventTimestamp\":1562599755,\"eventIndex\":6,\"sessionId\":\"f58179a375290599dde17f7c6d546d78\",\"versionId\":\"0.2.0\",\"eventEnvironment\":\"testing\",\"eventAttributes\":{\"playerId\": 12345678}}" "http://0.0.0.0:8080/v1/event?key={{your_analytics_api_key}}&event_schema=improbable&event_category=external&event_environment=debug&session_id=f58179a375290599dde17f7c6d546d78"
```

A successful response looks like:

```json
{"statusCode":200}
```

6\. Stop execution of your local Pod by pressing `Ctrl + C` in the terminal window where it is running. Alternatively, in a different window, run the following commands in order:

```sh
docker ps # Copy {{container_id_1}} & {{container_id_2}}
docker kill {{container_id_1}} {{container_id_2}}
```

Congratulations, you have successfully run the Analytics Pipeline locally!
