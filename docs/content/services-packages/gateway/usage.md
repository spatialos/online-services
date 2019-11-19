# Gateway (including matchmaking): usage
<%(TOC)%>

The full connection flow goes something like this:

1. Talk to [PlayFab](https://api.playfab.com/docs/tutorials/landing-players/best-login) to get a token.
2. Exchange the PlayFab token for a player identity token (PIT) via the PlayFab Auth service you deployed.
3. Using the PIT as an auth header, create a new party (or join an existing one) via the `party` service.
4. Send a request to the Gateway to join the queue for a given game type.
5. Repeatedly check with the Gateway's Operations service whether you have a match for your party. When you do, you'll receive a login token and deployment name. You can use these to connect using the [normal SpatialOS flow](https://docs.improbable.io/reference/latest/shared/auth/integrate-authentication-platform-sdk#4-connecting-to-the-deployment).

We provide a [sample client](http://github.com/spatialos/online-services/tree/master/services/csharp/SampleClient) that demonstrates this flow up to and including obtaining a login token.

1\. Navigate to `/services/csharp/SampleClient` and run:

```bash
dotnet run -- --google_project "{{your_google_project_id}}" --playfab_title_id "{{your_playfab_title_id}}"
```

If you have set everything up correctly, you should see the script log in to PlayFab, obtain a PIT, create a party and then queue for a game. It won't get a match just yet though - you don't have any deployments that fit the matcher's criteria.

> If you encounter errors at this step relating to authentication, go back to step 4 and ensure you configured the Kubernetes files with the correct values.

2\. Start a deployment in the [usual way](https://docs.improbable.io/reference/latest/shared/deploy/deploy-cloud) - it doesn't matter what assembly or snapshot you use. You can leave the sample client running if you like.

3\. When the deployment is running and fully functional, add the `match` and `ready` tags to the deployment.

`match` is the "game type" tag you’re using for this example, and `ready` informs the matcher that the deployment is ready to accept players. You should quickly see the sample client script terminate, printing out the name of your deployment and a matching login token. You'll also see that the `ready` tag has been replaced by `in_use`.

Congratulations - you've deployed the Gateway together with the Analytics Pipeline and PlayFab Auth successfully!

![]({{assetRoot}}img/services-packages/gateway/demo.gif)

## Next steps

Next, you can customize the matcher logic to fit the needs of your game, or check out-of-the-box analytics events from the Gateway [in BigQuery](https://console.cloud.google.com/bigquery) using SQL:

```
SELECT *
FROM events.events_gcs_external
WHERE (eventSource LIKE 'gateway_%' OR eventSource = 'playfab_auth')
ORDER BY eventTimestamp DESC
LIMIT 100
;
```

There are three documents we recommend looking at next:

* **Deployment Pool**
If you're making a session-based game like an arena shooter, you might want to deploy a Deployment Pool - see the [Deployment Pool documentation]({{urlRoot}}/content/services-packages/deployment-pool/overview) for more information.
* **Local development**
The GDK for Unreal, the GDK for Unity and the Worker SDK have the option to run your game on your local development machine as if it were in the cloud. This is useful for faster development and testing iteration. You can do the same with Online Services. See the [local development]({{urlRoot}}/content/services-packages/gateway/local) guide if you're planning to use local deployments to test Online Services.
* **Analytics Pipeline**
As part of this quickstart guide you deployed the Analytics Pipeline, to capture out-of-the-box analytics events from the Gateway. You might want to start sending additional events, such as those from within your game, onto the same pipeline. See the [Analytics Pipeline documentation]({{urlRoot}}/content/services-packages/analytics-pipeline/overview) for more information.

<br/>------------<br/>
_2019-07-16 Page added with limited editorial review_
