# Setup
<%(TOC)%>

>**Note**: The Online Services require you to have a [SpatialOS](https://docs.improbable.io) project. The Services support any SpatialOS project, whether you have created it using Unreal engine with the [GDK for Unreal](https://docs.improbable.io/unreal), Unity using the [GDK for Unity](https://docs.improbable.io/unity), or [your own engine](https://docs.improbable.io/reference/latest/shared/byoe/introduction).

</br>
**Want to get up and running quickly?** </br>
If you want to get something up and running right away, you can follow the [Quickstart]({{urlRoot}}/content/get-started/quickstart-guide/introduction) - it includes the set up dependencies listed in this set up guide.

### Dependencies


The Online Services tools assume very little prior knowledge of cloud infrastructure technologies. However, there are a few things you need:

#### SpatialOS

* A SpatialOS project. </br></br
The project can be one you have created with either the [GDK for Unreal](https://docs.improbable.io/unreal), the [GDK for Unity](https://docs.improbable.io/unity), or [your own engine](https://docs.improbable.io/reference/latest/shared/byoe/introduction).</br>
(If you don't have a project, you can follow the [GDK for Unreal Example Project](https://docs.improbable.io/unreal/latest/content/get-started/dependencies) or the [GDK for Unity FPS Starter Project](https://docs.improbable.io/unity/latest/projects/fps/get-started/get-started).)</br>
Note that your project doesn't need to be deployed to SpatialOS to set up Online Services but you do need a local or cloud deployment to test matchmaking.</br></br>
* The SpatialOS Tools. </br></br>
If you have a project, you will have the SpatialOS Tools installed. However, if you need to get them again, you can follow the SpatialOS Tools installation guides for [Windows](https://docs.improbable.io/reference/latest//shared/setup/win), [Mac](https://docs.improbable.io/reference/latest/shared/setup/mac), or [Linux](https://docs.improbable.io/reference/latest/shared/setup/linux).

#### Cloud hosting

* You need cloud hosting _in addition_ to your SpatialOS game deployment hosting.</br></br>
We recommend you set up a [Google Cloud Platform](https://console.cloud.google.com) project. The services themselves are platform-agnostic and should run anywhere; however, the extra configuration we have provided for setting up the cloud infrastructure is Google-specific in places. </br>
Note that you can port these configurations to run on [Amazon AWS](https://aws.amazon.com/), [Microsoft Azure](https://azure.microsoft.com/en-us/), [Alibaba Cloud](https://www.alibabacloud.com/) or any cloud hosting service. </br>
**Tip:** If you use Google Cloud Platform, install [Google Cloud SDK](https://cloud.google.com/sdk/) - useful to push built images up to your Google Cloud project.

#### Install

* [.NET Core](https://dotnet.microsoft.com/download/dotnet-core) - required to build and run the C# services.
* [Docker](https://docs.docker.com/install/) - to build the images.
* [kubectl](https://kubernetes.io/docs/tasks/tools/install-kubectl/) - used to deploy services to a cloud Kubernetes instance.
* _(Optional)_ [Docker Compose](https://docs.docker.com/compose/install/) - useful for running the services locally.

#### The repository
* Fork or clone the Online Services repository: [github.com/spatialos/online-services](http://github.com/spatialos/online-services).</br>
We recommend you create a fork of the repository so that you can make whatever customizations you want.


<%(Nav hide="next")%>
<%(Nav hide="prev")%>

<br/>------------<br/>
_2019-07-16 Page added with limited editorial review_
[//]: # (TODO: https://improbableio.atlassian.net/browse/DOC-1135)
