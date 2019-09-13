# Quickstart: 3. Build your service images

We're going to use Docker to build our services as containers, then push them up to our Google Cloud project's container registry. To start, we need to configure Docker to talk to Google. Run:

```
gcloud auth configure-docker
```

You also need to ensure your `gcloud` client is authenticated properly:

```
gcloud auth login
```

Now we can build and push the Docker images for our services. Navigate to the directory where the Dockerfiles are kept (`/services/docker`). We're going to build the images for each of the services we want to deploy, `gateway`, `gateway-internal`, `party`, `playfab-auth` and `sample-matcher`.

Build the images like this, replacing the `{{your_google_project_name}}` part with the name of your Google Cloud project:

```bash
docker build -f ./gateway/Dockerfile -t "gcr.io/{{your_google_project_name}}/gateway" --build-arg CONFIG=Debug ..
docker build -f ./gateway-internal/Dockerfile -t "gcr.io/{{your_google_project_name}}/gateway-internal" --build-arg CONFIG=Debug ..
docker build -f ./party/Dockerfile -t "gcr.io/{{your_google_project_name}}/party" --build-arg CONFIG=Debug ..
docker build -f ./playfab-auth/Dockerfile -t "gcr.io/{{your_google_project_name}}/playfab-auth" --build-arg CONFIG=Debug ..
docker build -f ./sample-matcher/Dockerfile -t "gcr.io/{{your_google_project_name}}/sample-matcher" --build-arg CONFIG=Debug ..
```

What's happening here?

- The `-f` flag tells Docker which Dockerfile to use. A Dockerfile is like a recipe for cooking a container image. We're not going to dive into the contents of Dockerfiles in this guide, but you can read more about them in the [Docker documentation](https://docs.docker.com/engine/reference/builder/) if you're interested.
- The `-t` flag is used to name the image. We want to give it the name it'll have on the container store, so we use this URL-style format. We can optionally add a **tag** at the end in a `name:tag` format; if no tag is provided then `latest` will be used, which is the case here.
- The `--build-arg` is used to provide variables to the Dockerfile - in this case we're instructing `dotnet` to do a Debug rather than Release build.
- The `..` path at the end tells Docker which directory to use as the build context. We use our services root, so that the builder can access our C# service sources.

Once you've built all the images, you can push them up to the cloud:

```bash
docker push "gcr.io/{{your_google_project_name}}/gateway"
docker push "gcr.io/{{your_google_project_name}}/gateway-internal"
docker push "gcr.io/{{your_google_project_name}}/party"
docker push "gcr.io/{{your_google_project_name}}/playfab-auth"
docker push "gcr.io/{{your_google_project_name}}/sample-matcher"
```

Have a look at your [container registry on the Google Cloud Console](https://console.cloud.google.com/gcr) - you should see your built images there.

![]({{assetRoot}}img/quickstart/gcr.png)

<%(Nav)%>

<br/>------------<br/>
_2019-07-16 Page added with limited editorial review_
[//]: # (TODO: https://improbableio.atlassian.net/browse/DOC-1135)
