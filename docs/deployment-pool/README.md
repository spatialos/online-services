# Deployment Pool
This document serves as a technical overview of the Deployment Pool component. 

You should have some idea of SpatialOS terminology before reading this document.

## What is the Deployment Pool?
The Deployment Pool component maintains game deployments in a ready-to-go state. It is useful if you want players to be able to jump into a game or between levels with minimal wait times. It implements a basic algorithm to do this: Once a deployment is no longer considered ready-to-go, a new one is started to replace it.

The Pool is implemented as a long running process. It periodically polls the SpatialOS Platform APIs to find the current state and takes actions to bring the state in line with expectations. The current actions are as follows:

| Action       | Purpose      |
|--------------|--------------|
| `create`     | Creates a new deployment for the Pool. This is generated when there are less ready-to-go deployments than required. |
| `delete`     | Shuts down a running deployment. This is generated when a deployment is marked as "completed" and so is no longer required. A deployment puts itself in this state. |
| `update`     | Changes the metadata about a deployment once a process has finished. This is usually generated when a deployment has finished starting up and can be transitions to the ready-to-go state. |

## Algorithm
The Deployment Pool algorithm is currently very basic. It maintains a constant number of ready-to-go deployments and does not attempt to monitor player load or capacity and, as such, is suceptible to spikes in traffic.

The Pool maintains state with deployment tags. The tags can be viewed in the console to see the state of the Pool at any time. The tags used are as follows:

Note these are subject to change in future versions.

| Tag         | Purpose |
|-------------|---------|
| `ready`     | A deployment can be used by players. |
| `starting`  | A deployment has been started but has not yet completed all start up actions. |
| `completed` | Added by the deployment itself to indicate it has finished running. For example, once a game session is over. |
| `stopping`  | A deployment is in the process of being shut down. |

## Configuration parameters
The Deployment Pool requires information about deployments it needs to start. These must be available beforehand in order for the Pool to operate.

| Parameter           |            | Purpose |
|---------------------|------------|---------|
| `Deployment prefix` |            | Deployments created by the Pool are allocated a random name. Use this to add a custom prefix to all deployments. |
| `Minimum Ready Deployments` |    | The number of "ready-to-go" deployments to maintain. Defaults to 3. |
| `Match type`        | `required` | A string representing the type of deployment this Pool will look after. For example, "fps", "session", "dungeon0". |
| `SpatialOS project` | `required` | The SpatialOS project to start deployments in. The Deployment Pool must have write access to this project to start deployments. |
| `SpatialOS refresh token` | `required` | A SpatialOS token which provides authentication for the Pool to use the SpatialOS Platform. |
| `Snapshot`          | `required` | The path to the deployment snapshot to start any deployments with. |
| `Launch config`     | `required` | The path to the launch configuration json file to start any deployments with. |
| `Assembly`          | `required` | The name of the assembly that is uploaded to the SpatialOS project this Pool is running against. |
