# Gateway (including matchmaking): local
<%(TOC)%>

When you are developing your game in SpatialOS, you can run it locally on your development machine as if it were in the cloud. This is useful for fast development and testing iterations. You can also run Online Services locally using `docker-compose`. You use this tool to start up multiple containers and ensure they're on the same network.

## Prerequisites

* This page assumes you have already [deployed the Gateway]({{urlRoot}}/content/services-packages/gateway/deploy).
* If you're on Windows, you need to take some additional steps to mount Docker volumes. These steps are in a separate [Docker volumes on Windows]({{urlRoot}}/content/workflows/docker-windows-volumes.md) guide.

## Deploy locally

In the beginning of this section you will need to note down a few values. We have labelled these `{{in_double_curly_brackets}}`, and will refer back to them afterwards.

1\. Navigate to [the Service accounts overview in the IAM section within the Cloud Console](https://console.cloud.google.com/iam-admin/serviceaccounts) for your Google project.

* Create and store a local JSON **and** P12 key from the service account named **Analytics GCS Writer**. Note down their local paths: `{{your_local_path_json_key_analytics_gcs_writer}}` and `{{your_local_path_p12_key_analytics_gcs_writer}}`.
* Create and store a local JSON key from the service account named **Analytics Endpoint**. Note down its local path: `{{your_local_path_json_key_analytics_endpoint}}`.

2\. Find [the API key for your Google project](https://console.cloud.google.com/apis/credentials) that you created in [step 3.1.3 of the Deploy section]({{urlRoot}}/content/services-packages/gateway/deploy#3-1-3-google-cloud-project-api-key) and store this in a local file. Note down its local path: `{{your_local_path_analytics_api_key}}`.

3\. Store the following text in a local file:

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

<%(Callout type="info" message="Ensure you preserve the exact indentation of the text snippet above when copying it into a local file.")%>

4\. Set the following environment variables:

| Environment variable | Value |
|----------------------|-------|
| `GOOGLE_SECRET_KEY_JSON_ANALYTICS_GCS_WRITER` | `{{your_local_path_json_key_analytics_gcs_writer}}` |
| `GOOGLE_SECRET_KEY_P12_ANALYTICS_GCS_WRITER` | `{{your_local_path_p12_key_analytics_gcs_writer}}` |
| `GOOGLE_SECRET_KEY_JSON_ANALYTICS_ENDPOINT` | `{{your_local_path_json_key_analytics_endpoint}}` |
| `ANALYTICS_API_KEY` | `{{your_local_path_analytics_api_key}}` |
| `ANALYTICS_CONFIG` | `{{your_local_path_analytics_config}}` |
| `GOOGLE_PROJECT_ID` | `{{your_google_project_id}}` |
| `SPATIAL_PROJECT_NAME` | `{{your_spatialos_project_name}}` |
| `SPATIAL_REFRESH_TOKEN` | `{{your_spatialos_refresh_token}}` |
| `PLAYFAB_SECRET_KEY` | `{{your_playfab_secret_key}}` |
| `PLAYFAB_TITLE_ID` | `{{your_playfab_title_id}}` |

5\. From the root of the `online-services` repo, run the following command:

```bash
docker-compose -f services/docker/docker_compose_local_gateway.yml up
```

This runs local instances of each of the services, a local Redis instance and some ESP proxies to ensure HTTP support works locally. The services are available at the following ports:

| Service | gRPC Port | HTTP port |
|---------|-----------|-----------|
| Gateway | 4040 | 8080 |
| Party | 4041 | 8081 |
| PlayFab Auth | 4042 | 8082 |

6\. Verify the services are working correctly by using the `SampleClient` and passing the `--local` flag when running it. To do this, navigate to `/services/csharp/SampleMatcher` and run:

```bash
dotnet run -- --google_project "{{your_google_project_id}}" --playfab_title_id "{{your_playfab_title_id}}" --local
```

Note that running the services locally with HTTP still requires a Google Cloud Endpoints configuration to be deployed to the cloud; you'll need to be aware of this if making changes to the APIs themselves.

If you don't need HTTP support, you can remove the `esp` containers, then remap the ports to point directly at the containers.

7\. When you are finished, stop your local execution by pressing `Ctrl + C` in the terminal window where it is running. Alternatively, in a different window, run the following commands in order:

```sh
# Kill all running containers:
docker kill $(docker ps -q)
```

Congratulations, you have successfully run the Gateway locally!


<%(Nav hide="next")%>
<%(Nav hide="prev")%>

<br/>------------<br/>
_2019-07-16 Page added with limited editorial review_
