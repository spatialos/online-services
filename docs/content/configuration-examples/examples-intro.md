# Configuration examples overview
<%(TOC)%>

The [Online Services repository](http://github.com/spatialos/online-services) contains:

* services & packages - see [overview]({{urlRoot}}/content/services-packages/overview)
* configuration examples - see below.

The configuration examples are sample deployable containers, demonstrating how you might build functionality.

## Deployment Pool

A long-running process, deployed in your cluster, which will maintain a pool of ready-to-go deployments. Useful in session-based games where deployments are created and removed often.

- [Overview]({{urlRoot}}/content/services-packages/deployment-pool/overview)
- [Use guide]({{urlRoot}}/content/services-packages/deployment-pool/usage)
- [C# source](http://github.com/spatialos/online-services/tree/master/services/csharp/DeploymentPool)

## Sample matcher

A very naive matcher implementation in C#. Useful for demoing the matchmaking system and for seeing the rough structure of how a Matcher is implemented.

- [C# source](http://github.com/spatialos/online-services/tree/master/services/csharp/SampleMatcher)

## Sample client

A simple game client which you can use to demo the PlayFab auth and matchmaking systems, or validate that they are working.

- [C# source](http://github.com/spatialos/online-services/tree/master/services/csharp/SampleClient)

<%(Nav hide="next")%>
<%(Nav hide="prev")%>

<br/>------------<br/>
_2019-07-16 Page added with limited editorial review_
[//]: # (TODO: https://improbableio.atlassian.net/browse/DOC-1135)