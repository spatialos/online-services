# Deployment pool: use
<%(TOC)%>

## Prerequisites

The document assumes you have already completed the [Quickstart]({{urlRoot}}/content/get-started/quickstart) and have a working GKE cluster to deploy the Deployment pool on.

## Configuration

The Deployment pool requires information about the deployments it is going to start. These are passed as flags to the Deployment pool when it starts up. The full list of configuration parameters is as follows:

| Parameter           |            | Purpose |
|---------------------|------------|---------|
| `Deployment prefix` |            | Deployments created by the Pool are allocated a random name. Use this to further add a custom prefix to all these deployments, e.g. `pool-`. |
| `Minimum ready Deployments` |    | The number of "ready-to-go" deployments to maintain. Defaults to 3. |
| `Match type`        | `required` | A string representing the type of deployment this Pool will look after. For example, "fps", "session", "dungeon0". |
| `SpatialOS project` | `required` | The SpatialOS project to start deployments in. The Deployment Pool must have write access to this project to start deployments. |
| `SpatialOS refresh token` | `required` | A SpatialOS token which provides authentication for the Pool to use the SpatialOS Platform. |
| `Snapshot`          | `required` | The path to the deployment snapshot to start any deployments with. |
| `Launch config`     | `required` | The path to the launch configuration JSON file to start any deployments with. |
| `Assembly`          | `required` | The name of the previously uploaded assembly within the SpatialOS project this Pool is running against. |

## Build your deployment pool image

The Deployment Pool builds from the included Dockerfile to give you a docker image from the code. Navigate to the parent directory of the Dockerfile (`services/docker`) and run:

```bash
docker build -f ./deployment-pool/Dockerfile -t "gcr.io/[your project id]/deployment-pool" ..
```

Once the image is built, you can push it to your Google Cloud repository.

```bash
docker push "gcr.io/[your project id]/deployment-pool"
```

## Setup steps

To start a deployment, a previously uploaded assembly is required. This can be completed using the SpatialOS CLI from your SpatialOS Project files. (See the CLI documentation in the [Tools: CLI](https://docs.improbable.io/reference/latest/shared/spatialos-cli-introduction) section of the SpatialOS documentation.)

### Upload an assembly

You need to upload your assembly to SpatialOS ahead of time so that the pool can access it when it starts deployments.
```bash
spatial upload [assembly id]
```
The assembly id the string you will need to pass to the Deployment Pool.

## Run locally

To test the deployment pool locally, you need a previously uploaded assembly, a snapshot file for your deployment and a launch configuration file.

You need to set the `SPATIAL_REFRESH_TOKEN` environment variable to your refresh token. You can do it like this on Windows Command Prompt:

```bat
: Note that you'll need to start a new Command Prompt after running this.
setx SPATIAL_REFRESH_TOKEN "[your refresh token]"
```

On other platforms:

```bash
export SPATIAL_REFRESH_TOKEN="[your refresh token]"

# & Use `$SPATIAL_REFRESH_TOKEN` instead of `%SPATIAL_REFRESH_TOKEN%` in the docker command below!
```

Once these are in place, you can start the deployment pool using

```bash
docker run -v [local path to launch config]:/launch-config/default_launch.json -v [local path to snapshot file]:/snapshots/default.snapshot -e SPATIAL_REFRESH_TOKEN=%SPATIAL_REFRESH_TOKEN% gcr.io/[your Google project id]/deployment-pool --project "[your SpatialOS project id]" --launch-config "/launch-config/default_launch.json" --snapshot "/snapshots/default.snapshot" --assembly-name "[your uploaded assembly name]" --minimum-ready-deployments 3
```

The refresh token is passed as an environment variable as it is a secret and shouldn't be passed in plaintext. It is recommended to set the secret up from an external source, for example from a properly secured local file, then use `cat my-spatial-refresh-token` in the command above to avoid storing it in your command history.

## Deploy the deployment pool in the cloud

As in the quickstart, we will need a Kubernetes configuration file to run the deployment pool in our cluster. Update the included `deployment-pool/deployment.yaml` to replace `{{your_google_project_name}}` where required.

As the deployment pool will be starting deployments, you will need to provide a launch configuration and a snapshot as local files in Kubernetes. We will use Kubernetes config maps for this purpose so the files can be mounted alongside a pod.

### Launch configuration

This file should already exist in your SpatialOS project directory. The default name is `default_launch.json`.
Upload it as a config map in Kubernetes so this file can be mounted later. If your file is not called "default_launch.json" you may need to edit the Kubernetes configuration before deploying.

```bash
kubectl create configmap launch-config --from-file "[local path to launch config]"
```

### Snapshot

This is a binary file which contains your latest game snapshot. This is usually called something like `default.snapshot`.
Again, upload it as a config map in Kubernetes so this file can be mounted later. If your file is not called "default.snapshot" you may need to edit the Kubernetes configuration before deploying.

```bash
kubectl create configmap snapshot --from-file "[local path to snapshot file]"
```

### Deploy and run

Apply the deployment pool configuration file to your cluster. Kubernetes will mount the Snapshot and Launch Configuration files within the Pod so the deployment pool will be able to read them. By default, these files are expected to be called "default_launch.json" and "default.snapshot". Edit the `deployment.yaml` if the files you uploaded have different names.

```bash
kubectl apply -f ./deployment-pool/deployment.yaml
```

<%(Nav hide="next")%>
<%(Nav hide="prev")%>

<br/>------------<br/>
_2019-07-16 Page added with limited editorial review_
[//]: # (TODO: https://improbableio.atlassian.net/browse/DOC-1135)
