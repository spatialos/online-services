# Services & packages overview
<%(TOC)%>

The [Metagame Services repository](http://github.com/spatialos/metagame-services) contains:

* services & packages - see below.
* configuration examples - see [overview]({{urlRoot}}/content/configuration-examples/examples-intro).

Each Metagame Service is an executable which runs in the cloud and each Metagame Service package is a discrete set of functionality which offers you set up as part of a Metagame Service.

## Services

Deployable cloud services for matchmaking and authentication.

**Matchmaking:** </br>
For matchmaking, you can use the Gateway Service. This consists of:

* Gateway
* Gateway-internal
* Party & invite

You can find out about the Gateway in the [Gateway overview]({{urlRoot}}/content/services-packages/gateway/gateway) documentation.

**Authentication:** </br>
For authentication, you can use the PlayFab Auth Service.

You can find out about PlayFab Auth in the [Quickstart guide]({{urlRoot}}/content/get-started/quickstart) documentation.

### Matchmaking - the Gateway

#### Gateway
The client-facing interface to the matchmaking system. Exposes two gRPC services: the Gateway service and a [Long-running Operations](https://github.com/googleapis/googleapis/blob/master/google/longrunning/operations.proto) service.

- [C# service](http://github.com/spatialos/metagame-services/services/csharp/Gateway)
- [Gateway proto definition](http://github.com/spatialos/metagame-services/services/proto/gateway/gateway.proto)
- [Long-running Operations proto definition](http://github.com/spatialos/metagame-services/services/proto/google/longrunning/operations.proto)

#### Gateway-internal

Used by the Gateway, Gateway-internal is the matcher-facing interface to the matchmaking service. Exposes a GatewayInternal gRPC service - with the default configuration this is only exposed to other services on the Kubernetes cluster.

- [C# service](http://github.com/spatialos/metagame-services/services/csharp/GatewayInternal)
- [Proto definition](http://github.com/spatialos/metagame-services/services/proto/gateway/gateway_internal.proto)

#### Party & invite

Also used by the Gateway, this is a separate, but related, service to the matchmaking system. Provides operations for the management of parties and invites to those parties. Exposes Party and Invite gRPC services.

- [C# service](http://github.com/spatialos/metagame-services/services/csharp/Party)
- [Party proto definition](http://github.com/spatialos/metagame-services/services/proto/party/party.proto)
- [Invite proto definition](http://github.com/spatialos/metagame-services/services/proto/party/invite.proto)

### Authentication - PlayFab Auth

A simple authentication server which validates a provided PlayFab ticket and returns a Player Identity Token (PIT).

- [C# service](http://github.com/spatialos/metagame-services/services/csharp/PlayFabAuth)
- [Proto definition](http://github.com/spatialos/metagame-services/services/proto/auth/playfab.proto)


## Packages

Discrete sets of functionality which you can set up as part of a Metagame Service.

All packages are namespaced with `Improbable.MetagameServices.*`. You can find these on NuGet, but they're also included in this repository and imported as `ProjectReference`s in the example services.

### Base.Server

A generic C# gRPC server. Provides convenience methods for mounting services and adding interceptors, as well as as logging and support for exporting metrics to a [Prometheus](https://prometheus.io/) instance.

This package doesn't include anything Improbable-specific; you can use it for any C# server.

- [Source & documentation](http://github.com/spatialos/metagame-services/services/csharp/Base.Server/)
- [`Base.Server` package on NuGet](https://www.nuget.org/packages/Improbable.MetagameServices.Base.Server)

### Base.Matcher

A base class for implementing a Gateway [Matcher]({{urlRoot}}/content/services-packages/gateway/gateway.md#matchers).

- [Source](http://github.com/spatialos/metagame-services/services/csharp/Base.Matcher/)
- [`Base.Matcher` package on NuGet](https://www.nuget.org/packages/Improbable.MetagameServices.Base.Matcher)

### Common

A collection of classes and utilities for building online services. This includes our data model, database client libraries, Platform SDK, PIT interceptors and more. Include this library if you're building an online service for a SpatialOS game.

- [Source](http://github.com/spatialos/metagame-services/services/csharp/Common)
- [`Common` on NuGet](https://www.nuget.org/packages/Improbable.MetagameServices.Common)

### Proto

A NuGet package of our compiled Protocol Buffers. Used to provide client or server interfaces for each of our APIs.

- [Source](http://github.com/spatialos/metagame-services/services/csharp/Proto)
- [`Proto` on NuGet](https://www.nuget.org/packages/Improbable.MetagameServices.Proto)

<%(Nav hide="next")%>
<%(Nav hide="prev")%>

<br/>------------<br/>
_2019-07-16 Page added with limited editorial review_
[//]: # (TODO: https://improbableio.atlassian.net/browse/DOC-1135)
