# Deployment Pool: local
<%(TOC)%>

## Prerequisites

* This page assumes you have already [deployed the Deployment Pool]({{urlRoot}}/content/services-packages/deployment-pool/deploy).
* If you're on Windows, there are some additional steps needed to mount Docker volumes. These steps are in a separate [Docker volumes on Windows]({{urlRoot}}/content/workflows/docker-windows-volumes.md) guide.

## Deploy locally without analytics

To test the Deployment Pool locally, you need a previously uploaded assembly, a snapshot file for your deployment and a launch configuration file.

0. Set the `SPATIAL_REFRESH_TOKEN` environment variable to the refresh token you created in Step 3. You can do it like this on Windows Command Prompt:

```bat
: Note that you'll need to start a new Command Prompt after running this.
setx SPATIAL_REFRESH_TOKEN "{{your_refresh_token}}"
```

On other platforms:

```bash
export SPATIAL_REFRESH_TOKEN="{{your_spatialos_refresh_token}}"

# & Use `$SPATIAL_REFRESH_TOKEN` instead of `%SPATIAL_REFRESH_TOKEN%` in the docker command below!
```

_Note that you need to input the actual token, not the path to the token!_

0. Once these are in place, you can start the Deployment Pool using:

```bash
docker run -v {{local_path_to_default_launch_config}}:/launch-config/default_launch.json -v {{local_path_to_default_snapshot_file}}:/snapshots/default.snapshot -e SPATIAL_REFRESH_TOKEN=%SPATIAL_REFRESH_TOKEN% gcr.io/{{your_google_project_id}}/deployment-pool --project "{{your_spatialos_project_name}}" --launch-config "/launch-config/default_launch.json" --snapshot "/snapshots/default.snapshot" --assembly-name "{{your_uploaded_assembly_name}}" --minimum-ready-deployments 3
```

As we are now running the Deployment Pool standalone, we will not capture any analytics just yet. This will happen once we deploy the Deployment Pool together with the Analytics Pipeline using `docker-compose`.

## Deploy locally with analytics

| Environment Variable                          | Value                                               |
|-----------------------------------------------|-----------------------------------------------------|
| `GOOGLE_PROJECT_ID`                           | `{{your_Google_project_id}}`                        |
| `GOOGLE_SECRET_KEY_JSON_ANALYTICS_GCS_WRITER` | `{{your_local_json_key_path_analytics_gcs_writer}}` |
| `GOOGLE_SECRET_KEY_P12_ANALYTICS_GCS_WRITER`  | `{{your_local_p12_key_path_analytics_gcs_writer}}`  |
| `GOOGLE_SECRET_KEY_JSON_ANALYTICS_ENDPOINT`   | `{{your_local_json_key_path_analytics_endpoint}}`   |
| `DEFAULT_LAUNCH_CONFIG`                       | `{{your_local_path_to_default_launch_config}}`      |
| `DEFAULT_SNAPSHOT_FILE`                       | `{{your_local_path_to_default_snapshot_file}}`      |
| `SPATIAL_PROJECT_NAME`                        | `{{your_spatialos_project_name}}`                   |
| `SPATIAL_ASSEMBLY_NAME`                       | `{{your_uploaded_assembly_name}}`                   |
| `SPATIAL_REFRESH_TOKEN`                       | `{{your_spatialos_refresh_token}}`                  |
| `GCP_API_KEY`                                 | `{{your_local_path_to_gcp_api_key}}`                |
| `ANALYTICS_CONFIG`                            | `{{your_local_path_to_analytics_config}}`           |

```sh
docker-compose -f services/docker/docker_compose_local_deployment_pool.yml up
```
