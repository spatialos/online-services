# Local Online Services: Guide
<%(TOC)%>

When you are developing your game in SpatialOS, you can run it locally, on your development machine as if it were in the cloud. This is useful for fast development and testing iterations.  You can also run Online Services locally. To run these Services locally, you use Docker Compose. You use this tool to start up multiple containers and ensure they're on the same network.

## Prerequisites

You'll need to have completed the [quickstart guide]({{urlRoot}}/content/get-started/quickstart-guide/introduction.md) already - specifically the Terraform section. This is because the proxy we use to provide HTTP support still needs to talk to your Google Cloud Endpoints. You'll also be using the Docker images built in that guide.

If you're on Windows, there are some additional steps needed to mount Docker volumes. These steps are in a separate [Docker volumes on Windows]({{urlRoot}}/content/workflows/docker-windows-volumes.md) guide.

## Configuring

First, you'll need to obtain a Google Service Account with the necessary permissions - this is used by the HTTP proxy to talk to the Cloud Endpoints we configured before. You can find instructions for how to do this in [Google's documentation](https://cloud.google.com/endpoints/docs/grpc/running-esp-localdev#create_service_account). Download the service account JSON key, put it somewhere and rename it to `service-account.json`.

Before running the services, we need to set some environment variables. These are described in the following table:

| Variable                      | Purpose |
|-------------------------------|---------|
| `GOOGLE_PROJECT_ID`           | The Google Cloud project you pushed your Endpoints configuration to in the quickstart guide. |
| `GOOGLE_SERVICE_ACCOUNT_PATH` | The path to a local directory containing `service-account.json`. |
| `PLAYFAB_TITLE_ID`            | The title ID of your PlayFab project. |
| `PLAYFAB_SECRET_KEY`          | Your PlayFab secret key as a string. For security, we recommend creating a new key just for running locally, which you can delete when you're finished with it. |
| `SPATIAL_PROJECT`             | The name of your SpatialOS project. |
| `SPATIAL_REFRESH_TOKEN`       | Your SpatialOS refresh token as a string (not a file path). You created this during the quickstart guide. |

Once this is set up, navigate to the `/services/docker` directory and run:

```bash
docker-compose -f ./docker_compose_local.yml up
```

This runs local instances of each of the services, a local Redis instance and some ESP proxies to ensure HTTP support works locally. The services are available at the following ports:

| Service      | gRPC Port | HTTP Port |
|--------------|-----------|-----------|
| Gateway      | 4040      | 8080      |
| Party        | 4041      | 8081      |
| PlayFab Auth | 4042      | 8082      |

You can verify the services are working correctly by using the `SampleClient` - just pass the `--local` flag when running it.

```bash
# From /services/csharp/SampleMatcher
dotnet run -- --google_project "[your Google project ID]" --playfab_title_id "[your PlayFab title ID]" --local
```

Please note that running the services locally with HTTP still requires a Google Cloud Endpoints configuration to be deployed to the cloud; you'll need to be aware of this if making changes to the APIs themselves.

If you don't need HTTP support, you can remove the `esp` containers, then remap the ports to point directly at the containers.


<%(Nav hide="next")%>
<%(Nav hide="prev")%>

<br/>------------<br/>
_2019-07-16 Page added with limited editorial review_
[//]: # (TODO: https://improbableio.atlassian.net/browse/DOC-1135)
