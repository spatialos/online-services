

# Quickstart: 1. Create a Google Cloud project

Go to the [project creation page](https://console.cloud.google.com/projectcreate) on the Google Cloud console. Give your project a name - remember, it can't be changed later!

There's a field to put in an organisation too. It's OK to leave this as `No organization` if you don't need this project to be part of one.

![]({{assetRoot}}img/quickstart/google-cloud-project.png)

It might take Google a couple of minutes to create your project. Once it's ready, open the navigation menu (the hamburger in the top-left) and go to **Kubernetes Engine**.

<%(#Expandable title="What's Kubernetes Engine")%>>Kubernetes Engine is Google's Kubernetes provision. </br>
Kubernetes, commonly known as k8s, is a container-based orchestration service; you give it your applications, housed in containers and provide some configuration, and it ensures your application stays available to users.  </br>
For example, if a container is defined as having 3 replicas, Kubernetes starts up 3 instances of that container. If one of them falls over or is stopped, Kubernetes immediately starts up another one for you. There will be more information on Kubernetes later in this guide but you can find out more in the [Kubernetes website](https://kubernetes.io/docs/concepts/overview/what-is-kubernetes/).<%(/Expandable)%>


Kubernetes Engine isn't free, but you can sign up to the free trial if you need to. You'll know it's ready to go when you can see this dialog box:

![]({{assetRoot}}img/quickstart/create-k8s-cluster.png)

It's possible to do a lot through Google Cloud's web console, but for this step we're going to use Terraform.

<%(Nav)%>

<br/>------------<br/>
_2019-07-16 Page added with limited editorial review_
[//]: # (TODO: https://improbableio.atlassian.net/browse/DOC-1135)



