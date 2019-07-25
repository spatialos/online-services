# Quickstart: Dependencies

This Quickstart guide shows you how to deploy the Gateway and PlayFab Auth services as an example. You can then use the same steps to use any other Online Services with your SpatialOS game project.
</br>
</br>
## Set up dependencies

**SpatialOS project and the SpatialOS Tools**</br>
To use the Online Services, you need a SpatialOS project and the SpatialOS Tools.

* The project</br>
The project can be one created with either the [GDK for Unreal](https://docs.imrobable.io/unreal), the [GDK for Unity](https://docs.imrobable.io/unity), or [your own engine](https://docs.improbable.io/reference/latest/shared/byoe/introduction).</br>
(If you don't have a project, you can follow the [GDK for Unreal Example Project](https://docs.improbable.io/unreal/latest/content/get-started/dependencies) or the [GDK for Unity FPS Starter Project](https://docs.improbable.io/unity/latest/projects/fps/get-started/get-started).)</br>
Note that your project doesn't need to be deployed to SpatialOS to set up Online Services but you do need a local or cloud deployment to test matchmaking.</br></br>
* The Tools </br>
If you have a project, you will have the SpatialOS Tools installed. However, if you need to get them again, you can follow the SpatialOS Tools installation guides for [Windows](https://docs.improbable.io/reference/latest//shared/setup/win), [Mac](https://docs.improbable.io/reference/latest/shared/setup/mac), or [Linux](https://docs.improbable.io/reference/latest/shared/setup/linux).

**Login to SpatialOS**</br>

* Make sure you are logged into [Improbable.io](https://improbable.io/). If you are logged in, you should see your picture in the top right of this page. </br>If you are not logged in, select **Sign in** at the top of this page and follow the instructions.</br>    

**Sign up for Google Cloud Platform (or a similar cloud storage account)**</br>

* You need cloud hosting _in addition_ to your SpatialOS game deployment hosting.</br>
These insructions use [Google Cloud Platform](https://console.cloud.google.com/getting-started) to run the Gateway and PlayFab Auth. You can adapt these instructions to run your project from whatever hosting provider you want, as long as it provides a [Kubernetes](https://kubernetes.io/) cluster. (Don't worry if you don't know what Kubernetes is yet.)</br>
If you are using Google Cloud Platform, make sure you log in to it.

**Sign up for a PlayFab account**</br>

* Sign up for a [PlayFab](https://playfab.com/) account if you don't have one. (It is free.)

**Install 3rd-party tools**</br>

* There are a few things you'll need to install.

    - [Docker](https://docs.docker.com/install/) - to build the images.
    - [Google Cloud SDK](https://cloud.google.com/sdk/) - we use this tool to push built images up to our Google Cloud project.
    - [Terraform](https://www.terraform.io/) - we use this to configure the different cloud services we use.
    - [kubectl](https://kubernetes.io/docs/tasks/tools/install-kubectl/) - used to deploy services to the cloud Kubernetes instance. It's included in Docker Desktop if you're on Windows.

**Fork or clone the repository**</br>

* Online Services repository: [github.com/spatialos/online-services](http://github.com/spatialos/online-services).
We recommend you create a fork of the repository so that you can make whatever customizations you want; use this as a base, rather than a comprehensive one-size-fits-all solution.