# Quickstart guide: 3. Build your service images

We're going to use Docker to build our services as containers, then push them up to our Google Cloud project's container registry. To start, we need to configure Docker to talk to Google.

Run:

```
gcloud components install docker-credential-gcr
```

followed by:

```
gcloud auth configure-docker
```

You also need to ensure your `gcloud` client is authenticated properly:

```
gcloud auth login
```

Now we can build and push the Docker images for our services. Navigate to the directory where the Dockerfiles are kept (`/services/docker`). We're going to build the images for each of the services we want to deploy, `gateway`, `gateway-internal`, `party`, `playfab-auth`, `sample-matcher` and  `analytics-endpoint`.

Build the images like this, replacing the `{{your_google_project_id}}` part with the name of your Google Cloud project:

```bash
docker build --file ./gateway/Dockerfile --tag "gcr.io/{{your_google_project_id}}/gateway" --build-arg CONFIG=Debug ..

docker build --file ./gateway-internal/Dockerfile --tag "gcr.io/{{your_google_project_id}}/gateway-internal" --build-arg CONFIG=Debug ..

docker build --file ./party/Dockerfile --tag "gcr.io/{{your_google_project_id}}/party" --build-arg CONFIG=Debug ..

docker build --file ./playfab-auth/Dockerfile --tag "gcr.io/{{your_google_project_id}}/playfab-auth" --build-arg CONFIG=Debug ..

docker build --file ./sample-matcher/Dockerfile --tag "gcr.io/{{your_google_project_id}}/sample-matcher" --build-arg CONFIG=Debug ..

docker build --file ./sample-matcher/Dockerfile --tag "gcr.io/{{your_google_project_id}}/analytics-endpoint" --build-arg CONFIG=Debug ..
```

What's happening here?

- The `--flag` flag tells Docker which Dockerfile to use. A Dockerfile is like a recipe for cooking a container image. We're not going to dive into the contents of Dockerfiles in this guide, but you can read more about them in the [Docker documentation](https://docs.docker.com/engine/reference/builder/) if you're interested.
- The `--tag` flag is used to name the image. We want to give it the name it'll have on the container store, so we use this URL-style format. We can optionally add a **tag** at the end in a `REGISTRY_NAME_OR_URL/OWNER/IMAGE:VERSION` format.
    - If you don't provide a `REGISTRY_NAME_OR_URL`, `docker push` and `docker pull` will assume you mean to interact with DockerHub, the open public registry.
    - If you don't provide a `VERSION`, latest is used. This is usually present, but depends on the person who owns the image and their release process.
- The `--build-arg` is used to provide variables to the Dockerfile - in this case we're instructing `dotnet` to do a Debug rather than Release build.
- The `..` path at the end tells Docker which directory to use as the build context. We use our services root, so that the builder can access our C# service sources.

Once you've built all the images, you can push them up to the cloud:

```bash
docker push "gcr.io/{{your_google_project_id}}/gateway"

docker push "gcr.io/{{your_google_project_id}}/gateway-internal"

docker push "gcr.io/{{your_google_project_id}}/party"

docker push "gcr.io/{{your_google_project_id}}/playfab-auth"

docker push "gcr.io/{{your_google_project_id}}/sample-matcher"

docker push "gcr.io/{{your_google_project_id}}/analytics-endpoint"
```

Have a look at your [container registry on the Google Cloud Console](https://console.cloud.google.com/gcr) - you should see your built images there.

![]({{assetRoot}}img/quickstart/gcr.png)

<%(Nav)%>

<br/>------------<br/>
_2019-07-16 Page added with limited editorial review_
[//]: # (TODO: https://improbableio.atlassian.net/browse/DOC-1135)
