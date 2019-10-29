# Services & packages overview
<%(TOC)%>

The [Online Services repository](http://github.com/spatialos/online-services) contains:

* services & packages - see below.
* configuration examples - see [`SampleClient`](https://github.com/spatialos/online-services/tree/master/services/csharp/SampleClient) and [`SampleMatcher`](https://github.com/spatialos/online-services/tree/master/services/csharp/SampleMatcher).

Each Online Service is an executable which runs in the cloud and each Online Service package is a discrete set of functionality which you set up as part of a Online Service.

## Services

Deployable cloud services for matchmaking and authentication.

### Gateway (including matchmaking)

For matchmaking, you can use the Gateway service. This consists of:

* **Gateway**

    The client-facing interface to the matchmaking system. Exposes two gRPC APIs: the Gateway API and a [long-running operations](https://github.com/googleapis/googleapis/blob/master/google/longrunning/operations.proto) API.

    - [Gateway proto definition](http://github.com/spatialos/online-services/tree/master/services/proto/gateway/gateway.proto)
    - [Long-running operations proto definition](http://github.com/spatialos/online-services/tree/master/services/proto/google/longrunning/operations.proto)
    - [Service implementation (C#)](http://github.com/spatialos/online-services/tree/master/services/csharp/Gateway)
<br><br>
* **Gateway-internal**

    Gateway-internal is the matcher-facing interface to the matchmaking service. It exposes a `GatewayInternal` gRPC API - with the default configuration, this is only exposed to other services on the Kubernetes cluster.

    - [Proto definition](http://github.com/spatialos/online-services/tree/master/services/proto/gateway/gateway_internal.proto)
    - [Service implementation (C#)](http://github.com/spatialos/online-services/tree/master/services/csharp/GatewayInternal)
<br><br>
* **Party & invite**

    Also used by the Gateway, this is a separate, but related, service to the matchmaking system. Provides operations for the management of parties and invites to those parties. Exposes `Party` and `Invite` gRPC APIs.

    - [Party proto definition](http://github.com/spatialos/online-services/tree/master/services/proto/party/party.proto)
    - [Invite proto definition](http://github.com/spatialos/online-services/tree/master/services/proto/party/invite.proto)
    - [Service implementation (C#)](http://github.com/spatialos/online-services/tree/master/services/csharp/Party)

You can find out about the Gateway in the [Gateway overview]({{urlRoot}}/content/services-packages/gateway) documentation.

### PlayFab Auth

For authentication, you can use the PlayFab Auth service. This is a simple authentication server that validates a PlayFab ticket and returns a Player Identity Token (PIT).

- [Proto definition](http://github.com/spatialos/online-services/tree/master/services/proto/auth/playfab.proto)
- [Service implementation (C#)](http://github.com/spatialos/online-services/tree/master/services/csharp/PlayFabAuth)

You can find out about PlayFab Auth in the [quickstart guide]({{urlRoot}}/content/get-started/quickstart-guide/introduction).

### Deployment Pool

Maintains game deployments in a ready-to-go state. It is useful if you want players to be able to jump into a game or between levels with minimal wait times.

You can find out about the Deployment Pool in the [Deployment Pool overview]({{urlRoot}}/content/services-packages/deployment-pool/overview).

### Analytics Pipeline

For analytics, you can use the Analytics Pipeline, which acts as a simple endpoint to send JSON analytics events to, which are persisted in a Cloud Storage bucket & ready for downstream analysis. See the [Analytics Pipeline documentation]({{urlRoot}}/content/services-packages/analytics-pipeline/overview).

## Packages

Discrete sets of functionality which you can set up as part of a Online Service.

All packages are namespaced with `Improbable.OnlineServices.*`. You can find these on NuGet, but they're also included in this repository and imported as `ProjectReference`s in the example services.

### Base.Server

A generic C# gRPC server. Provides convenience methods for mounting services and adding interceptors, as well as as logging and support for exporting metrics to a [Prometheus](https://prometheus.io/) instance.

This package doesn't include anything Improbable-specific; you can use it for any C# server.

- [Source & documentation](http://github.com/spatialos/online-services/tree/master/services/csharp/Base.Server/)
- [`Base.Server` package on NuGet](https://www.nuget.org/packages/Improbable.OnlineServices.Base.Server)

### Base.Matcher

A base class for implementing a Gateway [Matcher]({{urlRoot}}/content/services-packages/gateway.md#matchers).

- [Source](http://github.com/spatialos/online-services/tree/master/services/csharp/Base.Matcher/)
- [`Base.Matcher` package on NuGet](https://www.nuget.org/packages/Improbable.OnlineServices.Base.Matcher)

### Common

A collection of classes and utilities for building online services. This includes our data model, database client libraries, Platform SDK, PIT (<%(LinkTo doctype="reference" version="latest" path="/shared/auth/integrate-authentication-platform-sdk" title="Player Identity Token")%>) interceptors and more. Include this library if you're building an online service for a SpatialOS game.

- [Source](http://github.com/spatialos/online-services/tree/master/services/csharp/Common)
- [`Common` on NuGet](https://www.nuget.org/packages/Improbable.OnlineServices.Common)

### Proto

A NuGet package of our compiled Protocol Buffers. Used to provide client or server interfaces for each of our APIs.

- [Source](http://github.com/spatialos/online-services/tree/master/services/csharp/Proto)
- [`Proto` on NuGet](https://www.nuget.org/packages/Improbable.OnlineServices.Proto)

<%(Nav hide="next")%>
<%(Nav hide="prev")%>

<br/>------------<br/>
_2019-10-22 Page updated with limited editorial review: restructured and added Deployment Pool_<br>
_2019-07-16 Page added with limited editorial review_
[//]: # (TODO: https://improbableio.atlassian.net/browse/DOC-1135)
