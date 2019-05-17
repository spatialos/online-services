# Quickstart

This is our Quickstart guide. We'll get you up and running as quickly as we can. We're going to deploy the Gateway and PlayFab Auth services as an example, but you should be able to extend these instructions to any other included services.

## Prerequisites

Firstly, you'll need to be signed up to SpatialOS, and have it installed.

We're going to be using Google Cloud for this example. You can adapt these instructions to whatever you want - as long as it provides a [Kubernetes](https://kubernetes.io/) cluster. Don't worry if you don't know what Kubernetes is yet.

There are a few things you'll need to install.

- [Docker](https://docs.docker.com/install/) - to build the images.
- [Google Cloud SDK](https://cloud.google.com/sdk/) - we use this tool to push built images up to our Google Cloud project.
- [Terraform](https://www.terraform.io/) - we use this to configure the different cloud services we use.
- [kubectl](https://kubernetes.io/docs/tasks/tools/install-kubectl/) - used to deploy services to the cloud Kubernetes instance. Ships with Docker Desktop if you're on Windows.

You'll also need to sign up for a [PlayFab](https://playfab.com/) account if you don't have one. It's free!

## Creating a Google Cloud project

Go to the [project creation page](https://console.cloud.google.com/projectcreate) on the Google Cloud console. Give your project a name - remember, it can't be changed later!

There's a field to put in an organisation too. It's OK to leave this as `No organization` if you don't need this project to be part of one.

![](./img/quickstart/google_cloud_project.png)

It might take Google a couple of minutes to create your project. Once it's ready, open the navigation menu (the hamburger in the top-left) and go to **Kubernetes Engine**.

> Kubernetes Engine is Google's hosted Kubernetes offering. What is Kubernetes (or, commonly, k8s)?  It's a **container orchestration service**. Essentially, you give it some containerized applications, and some configuration, and it ensures your application stays up.  
> 
> For example, if a given container is defined as having 3 replicas, Kubernetes will start up 3 instances of that container. If one of them falls over or is stopped, Kubernetes will immediately start up another one for you.
>
> Don't worry if this doesn't make sense yet; you'll be walked through the whole thing.

Kubernetes Engine isn't free, but you can sign up to the free trial if you need to. You'll know it's ready to go when you can see this dialog box:

![](./img/quickstart/create_k8s_cluster.png)

It's possible to do a lot through Google Cloud's web console, but for this step we're going to use Terraform.

## Creating your infrastructure

[Terraform](https://www.terraform.io/) is a tool for configuring cloud infrastructure at a high level. It's a bit like a list of ingredients. In this case we want:

- A Kubernetes cluster.
- A MemoryStore instance (Google's hosted Redis), for the Gateway to use as a queue.

Our example configs are stored in [`/services/terraform`](../services/terraform). The files are:

- `variables.tf` - variables used for configuration, such as your Google Cloud project ID. You can define these in this configuration file, or leave them blank and provide them when you run `terraform plan` (we'll get there in a second).
- `provider.tf` - this file tells Terraform which cloud provider we're using.
- `gke.tf` - this instructs Terraform how to build our Kubernetes cluster.
- `memorystore.tf` - this defines the Google MemoryStore (Redis) instance.

You don't need to edit any files - run `terraform init` in this directory to ensure the right plugins are installed, then run `terraform plan -out "my_plan"`.

You'll be asked for some variables:
- Your cloud project ID. Note that this is the ID, not the display name.
- A zone; pick one from [here](https://cloud.google.com/compute/docs/regions-zones/), ensuring you pick a zone and not a region.
- A name for your cluster. This will be used in the name of the queue, too. You can put whatever you like here.

Terraform will print out a list of everything it's planning to configure for you, and store this as a file with whatever name you gave it earlier in place of `"my_plan"`.

You can review the plan by running `terraform show "my_plan"`.

Once you're ready to deploy, run `terraform apply "my_plan"`. This will take a few minutes. Once it's done, Terraform will print any output variables we defined in the configuration; in our case, that's the host IP of the new Redis instance.  
Make a note of it - we'll need it later. Or you can view outputs again by running `terraform output`.

If you look at your Cloud Console, you'll see we've now got a GKE cluster and a MemoryStore instance to work with. Now we just need something to run on them.

## Building your service images

We're going to use Docker to build our services as containers, then push them up to our Google Cloud project's container registry. To start, we need to configure Docker to talk to Google. Run:

```
gcloud auth configure-docker
```

You also need to ensure your `gcloud` client is authenticated properly:

```
gcloud auth login
```

Now we can build and push our images. Navigate to the directory where the Dockerfiles are kept (`/services/docker`). We're going to use the `gateway` container image as an example, but you'll want to do this for each of the images `gateway`, `gateway-internal` and `party`.

Build the image like this:

```
docker build -f ./gateway/Dockerfile -t gcr.io/[your project id]/gateway --build-arg CONFIG=Debug ..
```

What's happening here?
- The `-f` flag tells Docker which Dockerfile to use. A Dockerfile is like a recipe for cooking a container image. We're not going to dive into the contents of Dockerfiles in this guide, but you can read more about them in the [official documentation](https://docs.docker.com/engine/reference/builder/) if you're interested.
- The `-t` flag is used to name the image. We want to give it the name it'll have on the container store, so we use this URL-style format. We can optionally add a **tag** at the end in a `name:tag` format; if no tag is provided then `latest` will be used, which is the case here.
- The `--build-arg` is used to provide variables to the Dockerfile - in this case we're instructing `dotnet` to do a Debug rather than Release build.
- The `..` path at the end tells Docker which directory to use as the build context. We use our services root, so that the builder can access our C# service sources.

Once you've built all the images, you can push them up to the cloud. For example:

```
docker push gcr.io/[your project id]/gateway
```

Have a look at your container registry on the Cloud Console - you should see your built images there.

![](./img/quickstart/gcr.png)