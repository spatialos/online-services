# Deployment Pool: local
<%(TOC)%>

## Prerequisites

* This page assumes you have already [deployed the Deployment Pool]({{urlRoot}}/content/services-packages/deployment-pool/deploy).
* To run the Deployment Pool, you need a previously uploaded assembly, a snapshot file for your deployment and a launch configuration file.
* If you're on Windows, there are some additional steps needed to mount Docker volumes. These steps are in a separate [Docker volumes on Windows]({{urlRoot}}/content/workflows/docker-windows-volumes.md) guide.

## Overview

In this section you will execute the code of the Deployment Pool locally, first without analytics as a standalone container, and optionally afterwards together with analytics using `docker-compose` (which is already included with Docker). Even though you will run the code of these services locally, they will still need to be connected to the internet in order to be able to function as intended: the Deployment Pool for instance has to start up SpatialOS cloud deployments, and the Analytics Pipeline will have to store analytics events in Google Cloud Storage (GCS).

## Deploy locally without analytics

0. Set the `SPATIAL_REFRESH_TOKEN` environment variable to the refresh token you created in [Step 3 of the deploy section]({{urlRoot}}/content/services-packages/deployment-pool/deploy#step-3---create-secrets). You can do it like this on Windows Command Prompt:

```bat
: Note that you'll need to start a new Command Prompt after running this.
setx SPATIAL_REFRESH_TOKEN "{{your_refresh_token}}"
```

On other platforms:

```sh
export SPATIAL_REFRESH_TOKEN="{{your_spatialos_refresh_token}}"

# & Use `$SPATIAL_REFRESH_TOKEN` instead of `%SPATIAL_REFRESH_TOKEN%` in the `docker run ...` command below!
```

_Note that you need to input the actual token, not the path to the token!_

0. Once these are in place, you can start the Deployment Pool using the following template:

```sh
docker run -v {{your_local_path_default_launch_config}}:/launch-config/default_launch.json -v {{your_local_path_default_snapshot_file}}:/snapshots/default.snapshot -e SPATIAL_REFRESH_TOKEN=%SPATIAL_REFRESH_TOKEN% gcr.io/{{your_Google_project_id}}/deployment-pool --project "{{your_spatialos_project_name}}" --launch-config "/launch-config/default_launch.json" --snapshot "/snapshots/default.snapshot" --assembly-name "{{your_uploaded_assembly_name}}" --minimum-ready-deployments 3
```

Be sure to input your own values for everything in double curly brackets `{{ }}`.

Congratulations, you have successfully ran the Deployment Pool locally! If you want to also include analytics tracking while running the Deployment Pool locally, proceed to the next section.

## Deploy locally with analytics

0. Navigate to [the Service Account overview in the IAM section within the Cloud Console](https://console.cloud.google.com/iam-admin/serviceaccounts) for your Google project.
    - Create and store a local JSON **and** P12 key from the service account named **Analytics GCS Writer**. Note down their local paths: `{{your_local_path_json_key_analytics_gcs_writer}}` & `{{your_local_path_p12_key_analytics_gcs_writer}}`.
    - Create and store a local JSON key from the service account named **Analytics Endpoint**. Note down its local path: `{{your_local_path_json_key_analytics_endpoint}}`.

0. Grab [the API key for your Google project](https://console.cloud.google.com/apis/credentials) that you created [step 4.3.2 of the deploy section]({{urlRoot}}/content/services-packages/deployment-pool/deploy#432---gcp-api-key) and store this in a local file. Note down its local path: `{{your_local_path_gcp_api_key}}`.

0. Store the following text in a local file:

```
"*":
  "*":
   category: online_services
# The following events are disabled because they generate a very large amount of
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

| Environment Variable                          | Value                                               |
|-----------------------------------------------|-----------------------------------------------------|
| `GOOGLE_SECRET_KEY_JSON_ANALYTICS_GCS_WRITER` | `{{your_local_path_json_key_analytics_gcs_writer}}` |
| `GOOGLE_SECRET_KEY_P12_ANALYTICS_GCS_WRITER`  | `{{your_local_path_p12_key_analytics_gcs_writer}}`  |
| `GOOGLE_SECRET_KEY_JSON_ANALYTICS_ENDPOINT`   | `{{your_local_path_json_key_analytics_endpoint}}`   |
| `DEFAULT_LAUNCH_CONFIG`                       | `{{your_local_path_default_launch_config}}`         |
| `DEFAULT_SNAPSHOT_FILE`                       | `{{your_local_path_default_snapshot_file}}`         |
| `GCP_API_KEY`                                 | `{{your_local_path_gcp_api_key}}`                   |
| `ANALYTICS_CONFIG`                            | `{{your_local_path_analytics_config}}`              |
| `GOOGLE_PROJECT_ID`                           | `{{your_Google_project_id}}`                        |
| `SPATIAL_PROJECT_NAME`                        | `{{your_spatialos_project_name}}`                   |
| `SPATIAL_ASSEMBLY_NAME`                       | `{{your_uploaded_assembly_name}}`                   |
| `SPATIAL_REFRESH_TOKEN`                       | `{{your_spatialos_refresh_token}}`                  |


_Note that `SPATIAL_REFRESH_TOKEN` needs to be set to the actual token, not the path to the token!_

0. From within the root of the `online-services` repo, run the following command:

```sh
docker-compose -f services/docker/docker_compose_local_deployment_pool.yml up
```

0. When finished, stop your local execution by pressing _Cntrl + C_ in the terminal window it is running, or in a different window run the following commands in respective order:

```sh
docker ps # Copy {{container_id_1}} & {{container_id_2}} & {{container_id_3}}
docker kill {{container_id_1}} {{container_id_2}} {{container_id_3}}
```

Congratulations, you have successfully ran the Deployment Pool locally with analytics enabled!
