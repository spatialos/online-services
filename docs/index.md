# SpatialOS Metagame Services
<%(TOC)%>

<p align="center"><img src="{{assetRoot}}img/onlineservices.jpg" /></p>
[//]: # (TODO: New banner for section home page - on its way from BED (@Mushroom))

SpatialOS Metagame Services provide infrastructure around your game's [SpatialOS](https://docs.improbable.io) game server software and hosting; services such as authentication, matchmaking, and inventories. Metagame Services work with SpatialOS game projects created using Unreal engine with the [GDK for Unreal](https://docs.imrobable.io/unreal), or Unity with the [GDK for Unity](https://docs.imrobable.io/unity), or [your own engine](https://docs.improbable.io/reference/latest/shared/byoe/introduction).

The Metagame Services repository provides a suite of example gRPC (with additional HTTP support) services, packages and images. It gives you everything you need to start building online services to support your SpatialOS game. The Services are as unopinionated and generic as possible because you know best what your game requires. The primary language is C#, but we provide our protocol buffer files too so you can re-implement the services in whichever language you choose. The services support gRPC and HTTP.

**The Gateway as a matchmaking service**</br>
See the Improbable blogpost from 2019-june-17 on [Matchmaking with SpatialOS](https://improbable.io/blog/matchmaking-with-spatialos); it describes how you could use the Gateway as a matchmaking service.

## The Metagame Services repository
Repository on GitHub: [github.com/spatialos/metagame-services](https://github.com/spatialos/metagame-services)

We recommend you create a fork of the repository so that you can make whatever customizations you want; use this as a base, rather than a comprehensive one-size-fits-all solution.

## Where to start

* Find out more about the Gateway.</br>
Read the [Gateway guide]({{urlRoot}}/content/services-packages/gateway/gateway). This describes how the Gateway system works, and includes best practices for using it with your game.
 </br></br>
* Get started with the [Quickstart]({{urlRoot}}/content/get-started/quickstart) guide.
  </br></br>
* Find out what's inlcuded in the Metagame Services repository:</br>
    - services & packages - see documentation [overview]({{urlRoot}}/content/services-packages/services-intro)</br>
    - configuration examples - see documentation [overview]({{urlRoot}}/content/configuration-examples/examples-intro)


<%(Nav hide="next")%>
<%(Nav hide="prev")%>

<br/>------------<br/>
_2019-07-16 Page added with editorial review_
[//]: # (TODO: https://improbableio.atlassian.net/browse/DOC-1135)