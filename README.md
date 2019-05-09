# Improbable Online Services

![Build Status](https://badge.buildkite.com/4b2e4663ffac60c80d6c1e6b1d296b46155533a904ede73b0b.svg?branch=master) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

<p align="center"><img src="./docs/img/onlineservices.png" width="500px" /></p>

Improbable's SpatialOS provides a way for developers to easily build multiplayer game worlds. However, a modern online game requires additional infrastructure around the game server itself.

This repository provides a suite of example gRPC services, packages and images. The intention is to give you everything you need to start building online services to support your game, be it authentication, matchmaking, inventories or whatever else you can think of.

The intention is to be as unopinionated and generic as possible - you know best what your game requires. The primary language used is C#, but we provide our Protocol Buffer files too so you should be able to reimplement the services in whichever language you choose.

We encourage you to create a fork of this repo so that you can make whichever customisations you desire; use this as a base, rather than a comprehensive one-size-fits-all solution.

## Prerequisites

This repo and its documentation assume very little prior knowledge of cloud infrastructure technologies. However, there are a few things you should install.

- [.NET Core](https://dotnet.microsoft.com/download/dotnet-core) - required to build and run the C# services.
- [Docker](https://docs.docker.com/install/) - to build the images.
- [Docker Compose](https://docs.docker.com/compose/install/) - useful for running the services locally.
- [kubectl](https://kubernetes.io/docs/tasks/tools/install-kubectl/) - used to deploy services to a cloud Kubernetes instance.

We also recommend you set up a [Google Cloud](https://console.cloud.google.com) project. These services should be cloud-platform agnostic, but we have tested them on Google Cloud.

## Getting started

## Included in this repo

Trying to find something quickly? Here is a list of services, packages and sample configuration files included in this repository.

### Services

A selection of deployable services for authentication, matchmaking and more.

#### Gateway

The client-facing interface to the matchmaking system. Exposes two gRPC services: the Gateway service and a [Longrunning Operations](https://github.com/googleapis/googleapis/blob/master/google/longrunning/operations.proto) service.

- [C# service](./services/csharp/Gateway)
- [Gateway proto definition](./services/proto/gateway/gateway.proto)
- [Operations proto definition](./services/proto/google/longrunning/operations.proto)

#### GatewayInternal

The Matcher-facing interface to the matchmaking system. Exposes a GatewayInternal gRPC service - with the default configuration this is only exposed to other services on the Kubernetes cluster.

- [C# service](./services/csharp/GatewayInternal)
- [Proto definition](./services/proto/gateway/gateway_internal.proto)

#### Party & Invite

A separate, but related, service to the matchmaking system. Provides operations for the management of parties and invites to those parties. Exposes Party and Invite gRPC services.

- [C# service](./services/csharp/Party)
- [Party proto definition](./services/proto/party/party.proto)
- [Invite proto definition](./services/proto/party/invite.proto)

### Packages

All packages are namespaced with `Improbable.OnlineServices.*`. You can find these on NuGet if you like, but they're also included in this repo and imported as `ProjectReference`s in the example services.

#### Base.Server

A generic C# gRPC server. Provides convenience methods for mounting services and adding interceptors, as well as as logging and support for exporting metrics to a [Prometheus](https://prometheus.io/) instance.

This package doesn't include anything Improbable-specific; you can use it for any C# server.

* [Source & documentation](./services/csharp/Base.Server/)
* [`Base.Server` package on NuGet](https://www.nuget.org/packages/Improbable.OnlineServices.Base.Server)

#### Base.Matcher

A base class for implementing a Gateway [Matcher](./docs/Gateway.md#Matchers).

* [Source & documentation](./services/csharp/Base.Matcher/)
* [`Base.Matcher` package on NuGet](https://www.nuget.org/packages/Improbable.OnlineServices.Base.Matcher)

#### Common

A collection of classes and utilities for building online services. This includes our data model, database client libraries, Platform SDK, PIT interceptors and more. Include this library if you're building an online service for a SpatialOS game.

* [Source & documentation](./services/csharp/Common)
* [`Common` on NuGet](https://www.nuget.org/packages/Improbable.OnlineServices.Common)


#### Proto

A NuGet package of our compiled Protocol Buffers. Used to provide client or server interfaces for each of our APIs.

* [Source & documentation](./services/csharp/Proto)
* [`Proto` on NuGet](https://www.nuget.org/packages/Improbable.OnlineServices.Proto)

## License

This software is licensed under MIT. See the [LICENSE](./LICENSE.md) file for details.

## Contributors

The software in this repo was written by the following contributors:

- Joel Auterson
- Iulia Harasim
- Joshua McGhee
- Dominic Green
- Nik Gupta
- Ben Clive

## Contributing

We currently don't accept PRs from external contributors - sorry about that! We do accept bug reports and feature requests in the form of issues, though.