# Quickstart: 4. Set up Kubernetes

Kubernetes (or **k8s**) is configured using a tool called `kubectl`. Make sure you have it installed.

Before we do anything else we need to connect to our GKE cluster. The easiest way to do this is to go to the [GKE page](https://console.cloud.google.com/kubernetes/list) on your Cloud Console and click the 'Connect' button:

![]({{assetRoot}}img/quickstart/gke-connect.png)

This will give you a `gcloud` command you can paste into your shell and run. You can verify you're connected by running `kubectl cluster-info` - you'll see some information about the Kubernetes cluster you're now connected to.

#### Store your secrets

We've got two secrets we need to store on Kubernetes - our SpatialOS service account token, and our PlayFab server token.

> A "secret" is the k8s way of storing sensitive information such as passwords and API keys. It means the secret isn't stored in any configuration file or - even worse - your source control, but ensures your services will still have access to the information they need.

First, create a new Secret Key on PlayFab - you'll find this on the dashboard by going to Settings > Secret Keys. Give it a sensible name so you can revoke it later if you need to. Then run the following command, replacing the `{{your-playfab-secret}}` part with the secret you just created (it's a mix of numbers and capital letters):

```bash
kubectl create secret generic "playfab-secret-key" --from-literal="playfab-secret={{your-playfab-secret}}"
```

You should see:

```bash
secret/playfab-secret-key created
```

Great - our secret's on Kubernetes now, allowing it to be referred to from our configuration files.

We also need to create a SpatialOS service account. We provide a tool in this repo to do this for you, but first you need to make sure you're logged in to SpatialOS.

```bash
spatial auth login
```

The tool we're using lives at [`github.com/spatialos/online-services/tree/master/tools/ServiceAccountCLI`](https://github.com/spatialos/online-services/tree/master/tools/ServiceAccountCLI). You can read more about it in the [Service account CLI tool documentation]({{urlRoot}}/content/workflows/service-account-cli).

For now, you can navigate to the `tools/ServiceAccountCLI` directory and run the following command, replacing the `--project_name` parameter with the name of your SpatialOS project (you can change `--service_account_name` to whatever you want, but we've used "online_services_demo" as an example):

```bash
dotnet run -- create --project_name "[your SpatialOS project name]" --service_account_name "online_services_demo" --refresh_token_output_file=service-account.txt --lifetime=0.0:0 --project_write
```

We've set the lifetime to `0.0:0` here (i.e. 0 days, 0 hours, 0 minutes) - this just means it'll never expire. You might want to set something more appropriate to your needs.

Once the service account is generated, we push it up to k8s, like so:

```bash
kubectl create secret generic "spatialos-refresh-token" --from-file=./service-account.txt
```

#### Deploy to Google Cloud Platform

Now we need to edit the rest of the Kubernetes configuration files with variables that are specific to our deployment, such as our Google Project Name and the external IP addresses of our services.

This part's a little tedious, but you'll only need to do it once. Have a look through the various YAML files in the `k8s` directory and fill in anything `{{in_curly_brackets}}`. You can use the table below to work out what values go where - the IP addresses will have been provided to you when you applied your Terraform configuration, but you can also obtain them from the Google Cloud Console.

| Name | Description | Example Value |
| ---- | ----------- | ------------- |
| `{{your_google_project_id}}` | The ID of your Google Cloud project | `rhyming-pony-24680` |
| `{{your_spatialos_project_name}}` | The name of your Spatial OS project | `alpha_hydrogen_tape_345` |
| `{{your_playfab_title_id}}` | Your alphanumeric Playfab Title ID | `123A89` |
| `{{your_redis_host}}` | The IP address of your Memory Store | `10.1.2.3` |
| `{{your_gateway_host}}` | The IP address of your Gateway service | `123.4.5.6` |
| `{{your_party_host}}` | The IP address of your Party service | `123.7.8.9` |
| `{{your_playfab_auth_host}}` | The IP address of your Playfab Auth service | `123.10.11.12` |

You can use `git grep "{{.*}}"` to help find which files need editing.

> In the real world you'll probably use a templating system such as Jinja2, or simply find-and-replace with `sed`, to do this step more easily. Kubernetes doesn't provide any templating tools out of the box so we haven't used any here; feel free to pick your favourite if you so choose.

In Kubernetes we have many different types of configuration; here we use `ConfigMap`, `Deployment` and `Service`. We're not going to deep-dive into these right now; suffice to say that ConfigMaps hold non-sensitive configuration data, Deployments dictate what runs on a cluster, and Services dictate if and how they are exposed. You'll notice that `sample-matcher` doesn't have a Service configuration - this is because it doesn't expose any ports, being a long-running process rather than an actual web service.

Within the Google Cloud deployments, we define which containers to run in a pod. Our public services have an additional container, `esp` - this is a Google-provided proxy which is used for service discovery and HTTP transcoding.

Once you're done, you can run `git diff` to view your changes and check the values you entered. Any typos made here may cause issues later on, so it's worth spending a few moments to make sure your changes look good.

Next, navigate to the `k8s` directory and run:

```bash
kubectl apply -f config.yaml
kubectl apply -Rf gateway/
kubectl apply -Rf gateway-internal/
kubectl apply -Rf party/
kubectl apply -Rf playfab-auth/
kubectl apply -Rf sample-matcher/
```

These commands will recursively look through every file in the directories, generate configuration from them, and then push them to the cluster. You can then check your [Kubernetes Workloads page](https://console.cloud.google.com/kubernetes/workload) and watch as everything goes green. Congratulations - you've deployed successfully.

![]({{assetRoot}}img/workloads.png)

<%(Nav)%>

<br/>------------<br/>
_2019-07-16 Page added with limited editorial review_
[//]: # (TODO: https://improbableio.atlassian.net/browse/DOC-1135)
