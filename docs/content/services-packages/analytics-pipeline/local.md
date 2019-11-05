# Analytics Pipeline: local
<%(TOC)%>

## Prerequisites

* This page assumes you have already [deployed the Analytics Pipeline]({{urlRoot}}/content/services-packages/analytics-pipeline/deploy).
* If you're on Windows, there are some additional steps needed to mount Docker volumes. These steps are in a separate [Docker volumes on Windows]({{urlRoot}}/content/workflows/docker-windows-volumes.md) guide.

## Overview

When deploying the endpoint of the Analytics Pipeline on GKE, you will do this by deploying a pod (or multiple copies of a pod) that contains two containers:

- One container with your custom server code.
- A public ESP container provided by Google which runs everything related to [Cloud Endpoints](https://cloud.google.com/endpoints/).

In order to mimic deploying a pod that consists out of two containers locally, you will use [docker-compose](https://docs.docker.com/compose/), which is already included with Docker.

## Deploy locally

0. Navigate to [the Service Account overview in the IAM section within the Cloud Console](https://console.cloud.google.com/iam-admin/serviceaccounts) for your Google project.

- Create and store a local JSON **and** P12 key from the service account named **Analytics GCS Writer**. Note down their local paths: `{{your_local_json_key_path_analytics_gcs_writer}}` & `{{your_local_p12_key_path_analytics_gcs_writer}}`.
- Create and store a local JSON key from the service account named **Analytics Endpoint**. Note down its local path: `{{your_local_json_key_path_analytics_endpoint}}`.

0. Set the following environment variables:

| Environment Variable                          | Setting                                             |
|-----------------------------------------------|-----------------------------------------------------|
| `GOOGLE_PROJECT_ID`                           | `{{your_Google_project_id}}`                        |
| `GOOGLE_SECRET_KEY_JSON_ANALYTICS_GCS_WRITER` | `{{your_local_json_key_path_analytics_gcs_writer}}` |
| `GOOGLE_SECRET_KEY_P12_ANALYTICS_GCS_WRITER`  | `{{your_local_p12_key_path_analytics_gcs_writer}}`  |
| `GOOGLE_SECRET_KEY_JSON_ANALYTICS_ENDPOINT`   | `{{your_local_json_key_path_analytics_endpoint}}`   |

0. From within the root of the `online-services` repo, run the following command:

```bash
docker-compose -f services/docker/docker_compose_local_analytics_pipeline.yml up
```

0. Grab [the API key for your Google project](https://console.cloud.google.com/apis/credentials) that you created [step 3.1 of the deploy section]({{urlRoot}}/content/services-packages/analytics-pipeline/deploy#31---store-your-secret), which you need to pass via the **key** parameter in the url of your POST request: `{{gcp_api_key}}`.

0. Replace `{{gcp_api_key}}` with your API key in the curl request below and submit it in your terminal:

```sh
curl --request POST --header "Content-Type:application/json" --data "{\"eventSource\":\"client\",\"eventClass\":\"docs\",\"eventType\":\"endpoint_docker_compose\",\"eventTimestamp\":1562599755,\"eventIndex\":6,\"sessionId\":\"f58179a375290599dde17f7c6d546d78\",\"versionId\":\"2.0.13\",\"eventEnvironment\":\"testing\",\"eventAttributes\":{\"playerId\": 12345678}}" "http://0.0.0.0:8080/v1/event?key={{gcp_api_key}}&analytics_environment=testing&event_category=cold&session_id=f58179a375290599dde17f7c6d546d78"
```

A successful response will look like:

```json
{"code":200,"destination":{"formatted":"gs://cosmic-abbey-186211-analytics/data_type=json/analytics_environment=testing/event_category=cold/event_ds=2019-11-05/event_time=16-24/f58179a375290599dde17f7c6d546d78/2019-11-05T17:19:25Z-RL0EBT.jsonl"}}
```

0. Stop execution of your local pod by pressing _Cntrl + C_ in the terminal window it is running, or in a different window run the following commands in respective order:

```sh
docker ps # Copy {{container_id_1}} & {{container_id_2}}
docker kill {{container_id_1}} {{container_id_2}}
```

Congratulations, you have successfully ran the Analytics Pipeline locally!
