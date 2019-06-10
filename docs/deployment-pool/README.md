# Deployment Pool
This document serves as a technical overview of the Deployment Pool component. 

You should have some idea of SpatialOS terminology before reading this document.

## What is the Deployment Pool?
The Deployment Pool component maintains game deployments in a ready-to-go state. It is useful if you want players to be able to jump into a game or between levels with minimal wait times. It provides a number of deployments in this ready-to-go state: Once a deployment is no longer considered ready-to-go, a new one is started to replace it.

The Pool is implemented as a long running process. It periodically polls the SpatialOS Platform APIs to find the current state and takes actions to bring the state in line with expectations. The current actions are as follows:

| Action       | Purpose      |
|--------------|--------------|
| `create`     | Creates a new deployment for the Pool. This is generated when there are less ready-to-go deployments than required. |
| `delete`     | Shuts down a running deployment. This is generated when a deployment is marked as "completed" and so is no longer required. A deployment puts itself in this state. |
| `update`     | Changes the metadata about a deployment once a process has finished. This is usually generated when a deployment has finished starting up and can be transitions to the ready-to-go state. |

## Algorithm
The Deployment Pool algorithm is currently very basic. It maintains a constant number of ready-to-go deployments and simply replaces one when it becomes unavailable. It does not attempt to monitor player load or capacity and, as such, is vulnerable to spikes in traffic.

The Pool uses metadata on the deployment in the form of `tags` to show what state a deployment is in. The tags are as follows:

Note these are subject to change in future versions.

| Tag         | Purpose |
|-------------|---------|
| `ready`     | A deployment can be used by players. |
| `starting`  | A deployment has been started but has not yet completed all start up actions. |
| `completed` | Added by the deployment itself to indicate it has finished running. For example, once a game session is over. |
| `stopping`  | A deployment is in the process of being shut down. |

