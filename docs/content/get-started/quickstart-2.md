# Quickstart: 2. Create your infrastructure

[Terraform](https://www.terraform.io/) is a tool for configuring cloud infrastructure at a high level. It's a bit like a list of ingredients. In this case we want:

- A Kubernetes cluster.
- A MemoryStore instance (Google's hosted Redis), for the Gateway to use as a queue.

Before we use Terraform, we need to ensure our local `gcloud` tool is correctly authenticated with Google Cloud. Run:

```sh
gcloud auth application-default login
```

Our example configs are stored in [`github.com/spatialos/online-services/tree/master/services/terraform`](https://github.com/spatialos/online-services/tree/master/services/terraform). The files are:

- `variables.tf` - variables used for configuration, such as your Google Cloud project ID. You can define these in this configuration file, or leave them blank and provide them when you run `terraform plan` (we'll get there in a second).
- `provider.tf` - this file tells Terraform which cloud provider we're using.
- `gke.tf` - this instructs Terraform how to build our Kubernetes cluster.
- `memorystore.tf` - this defines the Google MemoryStore (Redis) instance.
- `ip.tf` - this is used to create static IP addresses for the services which need them.
- `services.tf` - enable required Google Cloud APIs and endpoints.

You don't need to edit any files - run `terraform init` in this directory to ensure the right plugins are installed, then run `terraform plan -out "my_plan"`.

You'll be asked for some variables:

- Your cloud project ID. Note that this is the ID, not the display name.
- A region; pick one from [google cloud](https://cloud.google.com/compute/docs/regions-zones/), ensuring you pick a region and not a zone (zones live within regions).
- A zone; ensure this zone is within your chosen region. For example, the zone `europe-west1-c` is within region `europe-west1`.
- A name for your cluster. This will be used in the name of the queue, too. You can put whatever you like here.

Terraform will print out a list of everything it's planning to configure for you, and store this as a file with whatever name you gave it earlier in place of `"my_plan"`.

You can review the plan by running `terraform show "my_plan"`.

Once you're ready to deploy, run `terraform apply "my_plan"`. This will take a few minutes. Once it's done, Terraform will print any output variables we defined in the configuration; in our case, that's the host IP of the new Redis instance, and our three new static IPs. Make a note of them - we'll need them later. Or you can view outputs again by running `terraform output`.

If you look at your Cloud Console, you'll see we've now got a GKE cluster and a MemoryStore instance to work with. You'll also see that [Endpoints](https://console.cloud.google.com/endpoints) have been created for the services; these provide a rudimentary DNS as well as in-flight HTTP-to-gRPC transcoding. Now we just need something to run on our cloud.

<%(Nav)%>

<br/>------------<br/>
_2019-08-07 Added Terraform gcloud authentication - with limited editorial review_
_2019-07-16 Page added with limited editorial review_
[//]: # (TODO: https://improbableio.atlassian.net/browse/DOC-1135)
