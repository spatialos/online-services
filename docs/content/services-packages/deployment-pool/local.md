# Deployment Pool: local
<%(TOC)%>

## Prerequisites

* This page assumes you have already [deployed the Deployment Pool]({{urlRoot}}/content/services-packages/deployment-pool/deploy).
* To run the Deployment Pool, you need a previously uploaded assembly, a snapshot file and a launch configuration file.
* If you're on Windows, you need to take some additional steps to mount Docker volumes. These steps are in a separate [Docker volumes on Windows]({{urlRoot}}/content/workflows/docker-windows-volumes.md) guide.

## Overview

In this section you will execute Deployment Pool code locally, first as a standalone container without analytics, and optionally afterwards with analytics using [`docker-compose`](https://docs.docker.com/compose/) (which is already included with Docker).

Even though you will run the code for these services locally, they still need to be connected to the internet in order to be able to function as intended: the Deployment Pool, for instance, needs to start up SpatialOS cloud deployments, and the Analytics Pipeline needs to store analytics events in Google Cloud Storage (GCS).

## Deploy locally without analytics

0. Set the `SPATIAL_REFRESH_TOKEN` environment variable to the refresh token you created in [step 4.3.1 of the Deploy section]({{urlRoot}}/content/services-packages/deployment-pool/deploy#431---spatialos-refresh-token).

To do this, on Windows Command Prompt, run:

```bat
setx SPATIAL_REFRESH_TOKEN "{{your_refresh_token}}"
```

Note that you need to start a new Command Prompt window after running this.

On other platforms, run:

```sh
export SPATIAL_REFRESH_TOKEN="{{your_spatialos_refresh_token}}"
```

<%(Callout type="info" message="Note that you need to enter the actual token, not the path to it.")%>

0. You can now start the Deployment Pool using the following template:

```sh
docker run -v {{your_local_path_default_launch_config}}:/launch-config/default_launch.json -v {{your_local_path_default_snapshot_file}}:/snapshots/default.snapshot -e SPATIAL_REFRESH_TOKEN=%SPATIAL_REFRESH_TOKEN% gcr.io/{{your_google_project_id}}/deployment-pool --project "{{your_spatialos_project_name}}" --launch-config "/launch-config/default_launch.json" --snapshot "/snapshots/default.snapshot" --assembly-name "{{your_uploaded_assembly_name}}" --minimum-ready-deployments 3
```

Remember to input your own values for everything `{{in_double_curly_brackets}}`. If you’re not using Windows Command Prompt, you need to use `$SPATIAL_REFRESH_TOKEN` instead of `%SPATIAL_REFRESH_TOKEN%`.

Congratulations, you have successfully run the Deployment Pool locally! If you want to include analytics tracking while running the Deployment Pool locally, move on to the next section.

## Deploy locally with analytics

0. Navigate to [the Service accounts overview](https://console.cloud.google.com/iam-admin/serviceaccounts) in the IAM section within the Cloud Console for your Google project.
    0. Create and store a local JSON **and** P12 key from the service account named **Analytics GCS Writer**. Note down their local paths: `{{your_local_path_json_key_analytics_gcs_writer}}` and `{{your_local_path_p12_key_analytics_gcs_writer}}`.
    0. Create and store a local JSON key from the service account named **Analytics Endpoint**. Note down its local path: `{{your_local_path_json_key_analytics_endpoint}}`.

0. Find [the API key for your Google project](https://console.cloud.google.com/apis/credentials) that you created in [step 4.3.2 of the Deploy section]({{urlRoot}}/content/services-packages/deployment-pool/deploy#432---google-cloud-project-api-key) and store this in a local file. Note down its local path: `{{your_local_path_analytics_api_key}}`.

0. Store the following text in a local file:

```
"*":
  "*":
   category: online_services
# The following events are disabled because they generate a very large number of
# events whenever there is no deployment available:
"match":
  "party_matching":
    disabled: true
  "party_requeued":
    disabled: true
  "player_matching":
    disabled: true
  "player_requeued":
    disabled: true
```

Note down its file path: `{{your_local_path_analytics_config}}`.

0. Set the following environment variables:

| Environment variable                          | Value                                               |
|-----------------------------------------------|-----------------------------------------------------|
| `GOOGLE_SECRET_KEY_JSON_ANALYTICS_GCS_WRITER` | `{{your_local_path_json_key_analytics_gcs_writer}}` |
| `GOOGLE_SECRET_KEY_P12_ANALYTICS_GCS_WRITER`  | `{{your_local_path_p12_key_analytics_gcs_writer}}`  |
| `GOOGLE_SECRET_KEY_JSON_ANALYTICS_ENDPOINT`   | `{{your_local_path_json_key_analytics_endpoint}}`   |
| `DEFAULT_LAUNCH_CONFIG`                       | `{{your_local_path_default_launch_config}}`         |
| `DEFAULT_SNAPSHOT_FILE`                       | `{{your_local_path_default_snapshot_file}}`         |
| `ANALYTICS_API_KEY`                           | `{{your_local_path_analytics_api_key}}`             |
| `ANALYTICS_CONFIG`                            | `{{your_local_path_analytics_config}}`              |
| `GOOGLE_PROJECT_ID`                           | `{{your_google_project_id}}`                        |
| `SPATIAL_PROJECT_NAME`                        | `{{your_spatialos_project_name}}`                   |
| `SPATIAL_ASSEMBLY_NAME`                       | `{{your_uploaded_assembly_name}}`                   |
| `SPATIAL_REFRESH_TOKEN`                       | `{{your_spatialos_refresh_token}}`                  |


<%(Callout type="info" message="Note that you need to set the `SPATIAL_REFRESH_TOKEN` to the actual token, not the path to the token.")%>

0. From the root of the `online-services` repo, run the following command:

```sh
docker-compose -f services/docker/docker_compose_local_deployment_pool.yml up
```

0. When you are finished, stop your local execution by pressing `Ctrl + C` in the terminal window where it is running. Alternatively, in a different window, run the following commands in order:

```sh
docker ps # Copy {{container_id_1}} & {{container_id_2}} & {{container_id_3}}
docker kill {{container_id_1}} {{container_id_2}} {{container_id_3}}
```

Congratulations, you have successfully run the Deployment Pool locally with analytics enabled!
