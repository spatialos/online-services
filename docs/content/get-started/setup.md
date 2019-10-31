# Setup
<%(TOC)%>

>**Note**: Most Online Services require you to have a [SpatialOS](https://docs.improbable.io) project. The Services support any SpatialOS project, whether you have created it using Unreal Engine with the [GDK for Unreal](https://docs.improbable.io/unreal), Unity using the [GDK for Unity](https://docs.improbable.io/unity), or [your own engine](https://docs.improbable.io/reference/latest/shared/byoe/introduction).

## Dependencies

You don't need any prior knowledge of cloud infrastructure technologies to start using the Online Services, but you'll need to learn about some of them as you progress.

There are also a few things you need to set up before getting started:

### SpatialOS

* **A SpatialOS project**

    The project can be one you have created with either the [GDK for Unreal](https://docs.improbable.io/unreal), the [GDK for Unity](https://docs.improbable.io/unity), or [your own engine](https://docs.improbable.io/reference/latest/shared/byoe/introduction).

    (If you don't have a project, you can follow the [GDK for Unreal Example Project](https://docs.improbable.io/unreal/latest/content/get-started/dependencies) or the [GDK for Unity FPS Starter Project](https://docs.improbable.io/unity/latest/projects/fps/get-started/get-started).)

    Note that your project doesn't need to be deployed to SpatialOS to set up Online Services but you do need a cloud deployment to test matchmaking.
<br><br>
* **The SpatialOS Tools**

    If you have a project, you will have the SpatialOS Tools installed. However, if you need to get them again, you can follow the SpatialOS Tools installation guides for [Windows](https://docs.improbable.io/reference/latest//shared/setup/win), [Mac](https://docs.improbable.io/reference/latest/shared/setup/mac), or [Linux](https://docs.improbable.io/reference/latest/shared/setup/linux).

### Cloud hosting

You need cloud hosting _in addition_ to your SpatialOS game deployment hosting. (This is billed according to your usage, and is entirely separate from any <%(LinkTo path="/shared/pricing-and-support/pricing-intro" title="SpatialOS costs" doctype="reference" version="latest")%>
.)


We recommend you set up a [Google Cloud Platform](https://console.cloud.google.com) project. The services themselves are platform-agnostic and should run anywhere; however, the extra configuration we have provided for setting up the cloud infrastructure is Google-specific in places.

Go to the [project creation page](https://console.cloud.google.com/projectcreate) on the Google Cloud console. Give your project a name - remember, it can't be changed later!

There's a field to put in an organisation too. It's OK to leave this as `No organization` if you don't need this project to be part of one.

![]({{assetRoot}}img/quickstart/google-cloud-project.png)

It might take Google a couple of minutes to create your project. Once it's ready, open the navigation menu (the hamburger in the top-left) and go to **Kubernetes Engine**.

<%(#Expandable title="What's Kubernetes Engine?")%>>Kubernetes Engine is Google's Kubernetes provision.<br/><br/>
Kubernetes, commonly known as k8s, is a container-based orchestration service; you give it your applications, housed in containers, and provide some configuration, and it ensures your application stays available to users.<br/><br/>
For example, if a container is defined as having 3 replicas, Kubernetes starts up 3 instances of that container. If one of them falls over or is stopped, Kubernetes immediately starts up another one for you. There will be more information on Kubernetes later in this guide but you can find out more on the [Kubernetes website](https://kubernetes.io/docs/concepts/overview/what-is-kubernetes/).<%(/Expandable)%>

Kubernetes Engine isn't free, but you can sign up to the free trial if you need to. You'll know it's ready to go when you can see this dialog box:

![]({{assetRoot}}img/quickstart/create-k8s-cluster.png)

> **Tip:** If you use Google Cloud Platform, install [Google Cloud SDK](https://cloud.google.com/sdk/) - useful to push built images up to your Google Cloud project.

Note that you can port these configurations to run on [Amazon AWS](https://aws.amazon.com/), [Microsoft Azure](https://azure.microsoft.com/en-us/), [Alibaba Cloud](https://www.alibabacloud.com/) or any cloud hosting service.

### Third-party tools

* [.NET Core](https://dotnet.microsoft.com/download/dotnet-core) - required to build and run the C# services.
* [Docker](https://docs.docker.com/install/) - to build the images.
* [kubectl](https://kubernetes.io/docs/tasks/tools/install-kubectl/) - used to deploy services to a cloud Kubernetes instance.
* _(Optional)_ [Docker Compose](https://docs.docker.com/compose/install/) - useful for running the services locally.
* [Google Cloud SDK](https://cloud.google.com/sdk/) - to push built images up to our Google Cloud project.
* [Terraform](https://www.terraform.io/) - to configure the different cloud services we use.

### The repository

Fork or clone the Online Services repository: [github.com/spatialos/online-services](http://github.com/spatialos/online-services).

We recommend you create a fork of the repository so that you can make whatever customizations you want.

<%(Nav hide="next")%>
<%(Nav hide="prev")%>

<br/>------------<br/>
_2019-10-22 Page updated with editorial review_<br>
_2019-07-16 Page added with limited editorial review_
