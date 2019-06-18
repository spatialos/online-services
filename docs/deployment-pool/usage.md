# Deployment Pool Usage

## Prerequesites

The document assumes you have already completed the quickstart and have a working GKE cluster to deploy the Deployment Pool on.

## Configuration

The Deployment Pool requires information about the deployments it is to start. These are passed as flags to the Deployment Pool when it starts up. The full list of configuration parameters is as follows:

| Parameter           |            | Purpose |
|---------------------|------------|---------|
| `Deployment prefix` |            | Deployments created by the Pool are allocated a random name. Use this to further add a custom prefix to all these deployments. |
| `Minimum Ready Deployments` |    | The number of "ready-to-go" deployments to maintain. Defaults to 3. |
| `Match type`        | `required` | A string representing the type of deployment this Pool will look after. For example, "fps", "session", "dungeon0". |
| `SpatialOS project` | `required` | The SpatialOS project to start deployments in. The Deployment Pool must have write access to this project to start deployments. |
| `SpatialOS refresh token` | `required` | A SpatialOS token which provides authentication for the Pool to use the SpatialOS Platform. |
| `Snapshot`          | `required` | The path to the deployment snapshot to start any deployments with. |
| `Launch config`     | `required` | The path to the launch configuration json file to start any deployments with. |
| `Assembly`          | `required` | The name of the previously uploaded assembly within the SpatialOS project this Pool is running against. |

## Building your deployment pool image

The Deployment Pool builds from the included Dockerfile to give you a docker image from the code.

```bash
docker build -f ./deployment-pool/Dockerfile -t "gcr.io/[your project id]/deployment-pool"
```

Once the image is built, you can push it to your Google Cloud repository. 

```bash
docker push "gcr.io/[your project id]/deployment-pool
```

## Setup steps
To start a deployment, a previously uploaded assembly is required. This can be completed using the `spatial` command line tool from your SpatialOS Project files.

### Uploading an Assembly
You need to upload your assembly to SpatialOS ahead of time so that the pool can access it when it starts deployments.
```bash
spatial upload [assembly id]
```
The assembly id the string you will need to pass to the Deployment Pool.

## Running locally

To test the Deployment Pool locally, you need a previously uploaded assembly, a snapshot file for your deployment and a launch configuration file.

Once these are in place, you can start the deployment pool using

```bash
SPATIAL_REFRESH_TOKEN=[your refresh token] docker run gcr.io/[your project id]/deployment-pool --project [your spatial project] --launch-config [path to your launch config] --snapshot [path to your snapshot file] --minimum-ready-deployments [number of deployments]
```

The refresh token is passed as an environment variable as it is a secret and shouldn't be passed in plaintext. It is recommended to set the secret up from an external source, for example from a properly secured local file, then use `cat my-spatial-refresh-token` in the command above to avoid storing it in your command history.

## Deploying the Deployment Pool in the cloud

As in the quickstart, we will need a kubernetes configuration file to run the Deployment Pool in our cluster. Update the included `deployment-pool.yaml` to replace `[your project id]` where required.

As the Deployment Pool will be starting deployments, you will need to provide a launch configuration and a snapshot as local files in Kubernetes. We will use Kubernetes configmaps for this purpose so the files can be mounted alongside a pod.

### Launch Configuration

This file should already exist in your SpatialOS project directory. The default name is `default_launch.json`.
Upload it as a config map in Kubernetes so this file can be mounted later. If your file is not called "default_launch.json" you may need to edit the Kubernetes configuration before deploying.
```bash
kubectl create configmap launch-config --from-file [local path to launch config]
```

### Snapshot

This is a binary file which contains your latest game snapshot. This is usually called something like `default.snapshot`. 
Again, upload it as a configmap in Kubernetes so this file can be mounted later. If your file is not called "default.snapshot" you may need to edit the Kubernetes configuration before deploying.
```bash
kubectl create configmap snapshot --from-file [local path to snapshot file]
```

### Deploy and Run

Apply the Deployment Pool configuration file to your cluster. Kubernetes will mount the Snapshot and Launch Configuration files within the Pod so the Deployment Pool will be able to read them. By default, these files are expected to be called "default_launch.json" and "default.snapshot". Edit the `deployment.yaml` if the files you uploaded had different names.

```bash
kubectl apply -f ./deployment-pool.yaml
```

