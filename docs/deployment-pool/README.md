# Deployment Pool

This document serves as a technical overview of the Deployment Pool component. This component is optional and is not required to use SpatialOS or the Gateway.

You should have some idea of SpatialOS terminology before reading this document.

## What is the Deployment Pool?

The Deployment Pool component maintains game deployments in a ready-to-go state. It is useful if you want players to be able to jump into a game or between levels with minimal wait times, as initialising game worlds can sometimes take a few minutes. It implements a basic algorithm to do this: As deployments become used by players (i.e. no longer "ready-to-go"), new deployments are started to replace them. In this way, it always maintains a buffer of available deployments for players to join.

The Pool is implemented as a long running process. It periodically polls the SpatialOS Platform APIs to find the current state of Deployments in a project and takes actions to bring the state in line with expectations. The current actions are as follows:

| Action       | Purpose      |
|--------------|--------------|
| `create`     | Creates a new deployment for the Pool. This is generated when there are fewer ready-to-go deployments than required. |
| `delete`     | Shuts down a running deployment. This is generated when a deployment is marked as "completed" and so is no longer required. A deployment puts itself in this state. |
| `update`     | Changes a deployment's metadata once a process has finished. This is usually generated when a deployment has finished starting up and can be transitioned to the ready-to-go state. |

## Algorithm

The Deployment Pool algorithm is currently very basic. It maintains a constant number of ready-to-go deployments and does not attempt to monitor player load or capacity and, as such, is susceptible to spikes in traffic. More advanced features will be implemented in the future.

The Pool maintains state with deployment tags. The tags can be viewed in the console to see the state of any pooled deployments at any time. The tags used are as follows:

| Tag         | Purpose |
|-------------|---------|
| `ready`     | A deployment can be used by players. |
| `starting`  | A deployment has been started but has not yet completed all start up actions. |
| `completed` | Added by the deployment itself to indicate it has finished running. For example, once a game session is over. |
| `stopping`  | A deployment is in the process of being shut down. |

*Note: these tags are subject to change in future versions*

The algorithm is as follows:
1. List all running deployments in a project
1. Check for deployments with the "ready" tag: These are available for players
1. Check for deployments with the "starting" tag: These are in the process of becoming available.
1. If the number of ready deployments + the number of starting deployments is less than the minimum required then start new deployments to fill the gap. These new deployments will have the "starting" tag.
1. Check the start-up state of deployments with the "starting" tag. If the state is "healthy" according to the SpatialOS platform then replace the "starting" tag with the "ready" tag. 
1. Check for deployments with the "completed" tag: These deployments have finished their game session and need shutting down.
1. Wait for 10 seconds and repeat from step 1.

# Caveats

The Deployment Pool is intentionally very basic and will not fit every use case out-of-the-box. Some 
* Deployments will require one full iteration of become "ready". This can add up to 10 seconds to the start up time.
* Spikes in player count will exhaust the Pool. As the Deployment Pool does not change the rate of deployment creation, the nunber of waiting players can be keep increasing.
* Deployments updates (including tag changes, starting and stopping) can take a short time to become available to the List call and can cause more than the expected number of deployments to start in very rare cases.
