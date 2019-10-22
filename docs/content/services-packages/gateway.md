# Gateway (including matchmaking)
<%(TOC)%>

This is a technical overview of the Gateway: its features, design and implementation. It's not a usability guide; if you want to set up your own instances of these services follow the [quickstart guide]({{urlRoot}}/content/get-started/quickstart-guide/introduction.md).

This guide describes the Gateway and any directly associated functionality; you can find other functionality in Online Services, such as a [Deployment Pool]({{urlRoot}}/content/services-packages/deployment-pool/overview) detailed separately.

**Concepts and terminology**</br>
To find out about terms used in this document and get an overview of SpatialOS terminology and approach to game servers, see the concepts guides: </br>

* [GDK for Unreal concepts guide](https://docs.improbable.io/unreal/latest/content/spatialos-concepts/introduction) (if you're using the GDK for Unreal)
* [SpatialOS concepts guide](https://docs.improbable.io/reference/latest/shared/concepts/spatialos) (if you're _not_ using the GDK for Unreal)

## Overview

You can use the Gateway service to get your authenticated players into the correct SpatialOS deployments. It provides a scalable, asynchronous queue, and a method for distributing your custom matchmaking logic; whether that's picking the first available deployment for players or sorting them by skill level, region or whatever your game requires.

The Gateway uses a gRPC microservices architecture, and has the following constituents:

| Constituent          | Purpose     |
|--------------------|-------------|
| `gateway`          | Provides the client-facing interface to the system; allows users to request to be queued and check their queue status. |
| `gateway-internal` | An internal-facing interface, used for matchmaking logic to request players from the queue and then assign them back to deployments. |
| `matcher`          | A long-running process (rather than a gRPC service) which contains your custom matchmaking logic. We provide a library, `Base.Matcher`, which you will use to create your own matchers. You will have at least one of these per game type. |
| `party`            | Hosts two gRPC services, `party` and `invite`, which are used to manage groups of players and invitations to those groups. |

The Gateway also uses the following third-party product and SpatialOS product - these are not included in the Online Services repository:

| Product          | Purpose   |
|--------------------|-----------|
| Redis              | A [Redis](https://redis.io) instance, used to store the queue of players, join requests, and party information. |
| SpatialOS Platform SDK       | This SDK consists of APIs used to build tools, workflows and services that integrate with the SpatialOS platform. The Gateway uses the Platform SDK to authenticate users and request information about running deployments. See the [Platform SDK documentation](https://docs.improbable.io/reference/latest/platform-sdk/introduction)in the SpatialOS SDK section of the SpatialOS documentation. |

This diagram shows how the Gateway is structured:

![]({{assetRoot}}img/gateway.png)

All services and matchers are designed to be horizontally scalable. Redis is the single source of truth in the system. The services are provided by this repository; matchers are to be built by the user, with a template class provided in the package [`Base.Matcher`](http://github.com/spatialos/online-services/tree/master/services/csharp/Base.Matcher).

The Gateway system is parties-first; users can only queue as part of a party. You can use parties of one player each to model solo matching. Each party has a leader: the leader can request the party be queued, or cancel the request, while any player in the party can check the status of that request.

Once the Gateway has assigned a party to a deployment, and the members of that party have picked up their assignment, the system is no longer concerned with the party. Moving between deployments requires a re-queue.

RPCs on the Gateway are authenticated using Player Identity Tokens (PITs); you can acquire one of these from developer auth - see [SpatialOS SDK](https://docs.improbable.io/reference/latest/shared/auth/development-authentication) documentation - or from your own game authentication server - see [SpatialOS SDK](https://docs.improbable.io/reference/latest/shared/auth/integrate-authentication-platform-sdk) documentation. The Online Services repository includes an example PlayFab Auth server.

We'll now look at each of the microservices in turn, in the rough order in which they will be used in matchmaking.

## `party` service

Before entering the queue, a player needs to be part of a party. The `party` and `invite` services provide mechanisms to work with parties. One can use the `CreateParty` RPC to create a party, then `CreateInvite` to invite players. Other players can check their invites periodically using `ListAllInvites`, then join parties with `JoinParty`.

Parties and invites are stored in the same Redis instance used by the rest of the Gateway.

The services also provide other convenience methods such as `KickPlayerFromParty` and `LeaveParty`; have a look at the [Party service](http://github.com/spatialos/online-services/tree/master/services/csharp/Party) in the repository for more details.

## `gateway` service

The `gateway` service provides the main client-facing interface to the system as a whole. It provides a `Join` RPC, used to enqueue a party (individuals can be modelled as a party of one).

It also hosts a [`longrunning.Operations`](https://godoc.org/google.golang.org/genproto/googleapis/longrunning) service, used to check the status of a join request and delete it if no longer wanted.

The leader of an existing party will call the `Join` RPC, providing a game type. This creates a `PartyJoinRequest` entry for the party, as well as a `PlayerJoinRequest` entry for each player, used to report individual status to clients - initially their `State` parameter is `Requested`. These data structures are defined in the [`DataModel`](http://github.com/spatialos/online-services/tree/master/services/csharp/DataModel) project. The `PartyJoinRequest` is added to a queue (a Redis [Sorted Set](https://redis.io/topics/data-types), sorted by join time) for its requested game type.

From this point it is the responsibility of the clients to periodically query the `gateway` service for their join status, using the `GetOperation` RPC. When a player requests a join request that has been resolved, a Login Token is created, and they are given this token and its corresponding deployment. The `PlayerJoinRequest` entry is deleted. When all players have retrieved the assigned deployment, the `PartyJoinRequest` entry is deleted.

## `gateway-internal` service

The [`gateway-internal` service](http://github.com/spatialos/online-services/tree/master/services/csharp/GatewayInternal) mirrors the `gateway` service inside the system, providing access to the join queue to matchers.

It exposes two RPCs: `PopWaitingParties` and `AssignDeployments`. A matcher will first obtain a set of queued parties via the `PopWaitingParties` RPC. Parties are removed from the specified (game type) queue using Redis `ZPOPMIN` (implemented in Lua as this function is only provided in newer versions of Redis). The `PlayerJoinRequest`s remain in Redis to service the status requests, and their `State` parameter is updated to `Matching`. The dequeued entries are returned to the matcher.

`PopWaitingParties` takes two parameters; game type and number. This number defines how many parties to pop from the queue. Crucially, if this number of parties are not available, nothing will be popped and returned. This provides a batching behaviour - if a game type requires 10 parties, for example, they won't be removed from the queue until that queue contains at least 10 parties.

Once a matcher has chosen a deployment for each of the parties, it calls the `AssignDeployments` RPC, sending back the party along with a status and a deployment name and ID. The status can be `MATCHED`, `ERROR` or `REQUEUED` - informing the `gateway-internal` service whether to requeue the party or to provide the waiting players with a deployment or error.

## Matchers

A matcher is a long-running process - rather than a service - with clients for both `gateway-internal` and the Platform SDK. It has two methods: `DoMatch`, which is called repeatedly, and `DoShutdown`, called to cleanly shut down the matcher and deal with any state it might be maintaining. These methods are provided as stubs in the `Base.Matcher` project.

Matcher logic is provided by the user, and so the mapping between parties and deployments is defined in user code. A matcher can choose whether or not to maintain internal state. It can also choose, if no deployments are available, to hold on to its obtained parties or send a `REQUEUED` assignment back to `gateway-internal` - the latter may be safer in the case of a crash, but could result in users waiting in the queue for longer, depending on implementation.

It's recommended to have more than one matcher per game type. The tick rate of the matcher, the number of parties it requests, and the number of matchers per game type are all variables that need to be chosen specifically for each game; as such the provided software is unopinionated as to these.

<%(Nav hide="next")%>
<%(Nav hide="prev")%>

<br/>------------<br/>
_2019-07-16 Page added with limited editorial review_