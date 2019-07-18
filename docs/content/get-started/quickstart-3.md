

# Quickstart: 3. Build your service images

We're going to use Docker to build our services as containers, then push them up to our Google Cloud project's container registry. To start, we need to configure Docker to talk to Google. Run:

```
gcloud auth configure-docker
```

You also need to ensure your `gcloud` client is authenticated properly:

```
gcloud auth login
```

Now we can build and push our images. Navigate to the directory where the Dockerfiles are kept (`/services/docker`). We're going to use the `gateway` container image as an example, but you'll want to do this for each of the images `gateway`, `gateway-internal`, `party`, `playfab-auth` and `sample-matcher`.

Build the image like this:

```bash
docker build -f ./gateway/Dockerfile -t "gcr.io/[your project id]/gateway" --build-arg CONFIG=Debug ..
```

What's happening here?
- The `-f` flag tells Docker which Dockerfile to use. A Dockerfile is like a recipe for cooking a container image. We're not going to dive into the contents of Dockerfiles in this guide, but you can read more about them in the [Docker documentation](https://docs.docker.com/engine/reference/builder/) if you're interested.
- The `-t` flag is used to name the image. We want to give it the name it'll have on the container store, so we use this URL-style format. We can optionally add a **tag** at the end in a `name:tag` format; if no tag is provided then `latest` will be used, which is the case here.
- The `--build-arg` is used to provide variables to the Dockerfile - in this case we're instructing `dotnet` to do a Debug rather than Release build.
- The `..` path at the end tells Docker which directory to use as the build context. We use our services root, so that the builder can access our C# service sources.

Once you've built all the images, you can push them up to the cloud. For example:

```bash
docker push "gcr.io/[your project id]/gateway"
```

Have a look at your container registry on the Cloud Console - you should see your built images there.

![]({{assetRoot}}img/quickstart/gcr.png)




<%(Nav)%>

<br/>------------<br/>
_2019-07-16 Page added with limited editorial review_
[//]: # (TODO: https://improbableio.atlassian.net/browse/DOC-1135)