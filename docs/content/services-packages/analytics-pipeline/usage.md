# Analytics Pipeline: usage
<%(TOC)%>

All Online Services have been [instrumented](https://en.wikipedia.org/wiki/Instrumentation_(computer_programming)) out-of-the-box. This means that whenever you deploy the Analytics Pipeline, you automatically capture analytics events originating from these services and can query them with BigQuery in your Google project.

You can however decide to forward more events to the Analytics Pipeline as well, for instance those originating from within your game(s).

## Using the endpoint

To send more data to the Analytics Pipeline, you can make simple `POST` requests with a JSON payload to your endpoint. We recommend the payload to be a JSON list of approximately 100 events.

The endpoint URL takes six parameters:

| Parameter | Required/Optional | Description |
|-----------|-------------------|-------------|
| `key` | Required | Must be tied to your Google project ([info](https://cloud.google.com/endpoints/docs/openapi/get-started-kubernetes#create_an_api_key_and_set_an_environment_variable)). |
| `event_environment` | Optional | The build configuration that the event was sent from, for example {`debug`, `profile`, `release`}. |
| `event_category` | Optional | Defaults to `external`. See [this section]({{urlRoot}}/content/services-packages/analytics-pipeline/usage#the-event-category-url-parameter) for more information about this parameter. |
| `event_ds` | Optional | Defaults to the current UTC date in YYYY-MM-DD format. If needed, you can specify a date that overrides this. |
| `event_time` | Optional | Defaults to the current UTC time period. If needed, you can set a time period that overrides this, must be one of `0-8`, `8-16` or `16-24`. |
| `session_id` | Optional | Should be set, otherwise defaults to `session-id-not-available`. |

The parameters in the table above (except for `key`) play a part in how the data ends up in the GCS bucket. In the example below, these parameters are denoted by `{{double_curly_brackets}}`):

> gs://[[gcs_bucket_name]]/data_type=[[data_type]]/event_environment={{event_environment}}/event_category={{event_category}}/event_ds={{event_ds}}/event_time={{event_time}}/{{session_id}}/[[timestamp]]-[[random_alphanum]]

Note that the endpoint will automatically:

* use the `[[gcs_bucket_name]]` of the analytics cloud storage bucket we created with Terraform.
* determine `[[data_type]]`, which can either be `json` (when valid JSON is `POST`ed) or `unknown` (otherwise).
* set the fields `[[timestamp]]` and `[[random_alphanum]]` (a random string to avoid collisions) as well.

Parameters in the example storage path above that the endpoint always deals with automatically are denoted by `[[double_square_brackets]]`.

### The event_category URL parameter

The `event_category` parameter is particularly important:

* When set to `native`, all data contained in the `POST` request is ingested into native BigQuery storage using [the analytics Cloud Function (`function-improbable-schema-.*`)](https://console.cloud.google.com/functions/list) you created, for enhanced query performance. Note that `native` is a completely arbitrary string, which you can easily change in [its Terraform configuration](https://github.com/spatialos/online-services/blob/analytics-docs/services/terraform/module-analytics/pubsub.tf).
* When set to anything else, all data contained in the `POST` request arrives in GCS, but is not by default ingested into native BigQuery storage. You can still access these analytics events with BigQuery by using GCS as an external data source.

## The Event Schema

Each analytics event, which is a JSON dictionary, should adhere to the following JSON schema:

| Root key | Type | Description |
|----------|------|-------------|
| `eventEnvironment` | string | The build configuration that the event was sent from, for example {`debug`, `profile`, `release`}. |
| `eventSource` | string | Source of the event, which for in-game events equates to worker type. |
| `sessionId` | string | A session identifier, which for in-game events is unique per worker instance session. |
| `versionId` | string | Version of the game build or online service. Should naturally sort from oldest to latest. |
| `eventIndex` | integer | Increments by one with each event per `sessionId`. Allows you to spot missing data. |
| `eventClass` | string | A higher order mnemonic classification of the event (for example `session`). |
| `eventType` | string | A mnemonic event description (for example `session_start`). |
| `playerId` | string | A player's unique identifier, if available. |
| `eventTimestamp` | float | The timestamp of the event, in Unix time. |
| `eventAttributes` | dict | You can capture anything else relating to this particular event in this attribute as a nested JSON dictionary. |

Keep the following in mind:

* keys should always be `camelCase`, whereas values should be `snake_case` whenever they are of the type `string`.
* root keys of the dictionary are always present for any event, except for `playerId`, which can for instance be missing pre-login and post-logout (client-side events) or for certain server-side events.
* you should nest anything that is custom to a particular event within `eventAttributes`.

## Example POST request

0. Create a local JSON file called `hello_world.json` and note down its local path: `{{local_path_json_payload}}`.

0. Insert the following JSON into the file:

```json
[{"eventEnvironment":"testing","eventSource":"client","sessionId":"f58179a375290599dde17f7c6d546d78","versionId":"0.2.0","eventIndex":0,"eventClass":"docs","eventType":"test","playerId":"12345678","eventTimestamp":1562599755,"eventAttributes":{"hello":"world"}},
{"eventEnvironment":"testing","eventSource":"client","sessionId":"f58179a375290599dde17f7c6d546d78","versionId":"0.2.0","eventIndex":1,"eventClass":"docs","eventType":"test","playerId":"12345678","eventTimestamp":1562599755,"eventAttributes":{"hello":"world"}}]
```

An example `POST` request to the analytics endpoint that includes invoking the analytics Cloud Function (`event_category=native`) looks like this:

```sh
curl --request POST --header "Content-Type:application/json" --data @{{local_path_json_payload}} "http://analytics.endpoints.{{your_google_project_id}}.cloud.goog:80/v1/event?key={{your_analytics_api_key}}&event_schema=improbable&event_category=native&event_environment={{event_environment}}&session_id={{session_id}}"
```

Starting the `--data` value with the `@` symbol means you are passing it a file.

<%(#Expandable title="Want to pass the <code>--data</code> payload as a string instead?")%>
```sh
curl --request POST --header "Content-Type:application/json" --data "[{\"eventEnvironment\":\"testing\",\"eventSource\":\"client\",\"sessionId\":\"f58179a375290599dde17f7c6d546d78\",\"versionId\":\"0.2.0\",\"eventIndex\":0,\"eventClass\":\"docs\",\"eventType\":\"test\",\"playerId\":\"12345678\",\"eventTimestamp\":1562599755,\"eventAttributes\":{\"hello\":\"world\"}},{\"eventEnvironment\":\"testing\",\"eventSource\":\"client\",\"sessionId\":\"f58179a375290599dde17f7c6d546d78\",\"versionId\":\"0.2.0\",\"eventIndex\":1,\"eventClass\":\"docs\",\"eventType\":\"test\",\"playerId\":\"12345678\",\"eventTimestamp\":1562599755,\"eventAttributes\":{\"hello\":\"world\"}}]" "http://analytics.endpoints.{{your_google_project_id}}.cloud.goog:80/v1/event?key={{your_analytics_api_key}}&event_schema=improbable&event_category=native&event_environment={{event_environment}}&session_id={{session_id}}"
```
<%(/Expandable)%>

A successful response looks like this:

```json
{"code":200,"destination":{"formatted":"gs://cosmic-abbey-186211-analytics/data_type=jsonl/event_schema=improbable/event_category=native/event_environment=debug/event_ds=2019-10-30/event_time=8-16/f58179a375290599dde17f7c6d546d78/2019-10-30T12:09:59Z-NVSNU4.jsonl"}}
```

To test this yourself, replace:

* `{{local_path_json_payload}}` with the local path of the JSON file you just created.
* `{{your_google_project_id}}` and `{{your_analytics_api_key}}` (created in [step 3.1 of the deploy section]({{urlRoot}}/content/services-packages/analytics-pipeline/deploy#3-1-store-your-secret)) with your own values.
* `{{event_environment}}` with `debug`.
* `{{session_id}}` with any made up session identifier (such as `f58179a375290599dde17f7c6d546d78`).

Then, navigate to [BigQuery](https://console.cloud.google.com/bigquery) and submit the following queries to check your results:

```
-- Querying the GCS bucket directly:
SELECT *
FROM `external.events_improbable_*`
WHERE eventClass = 'docs'
ORDER BY eventTimestamp DESC
LIMIT 100
;

-- Checking whether our Cloud Function correctly copied the events over into native BigQuery storage:
SELECT *
FROM `native.events_improbable_*`
WHERE event_class = 'docs'
ORDER BY event_timestamp DESC
LIMIT 100
;
```

## Next steps

Some options include:

* [Enabling SSL for Cloud Endpoints](https://cloud.google.com/endpoints/docs/openapi/enabling-ssl)
* [Running backfills]({{urlRoot}}/content/services-packages/analytics-pipeline/backfill)
* [Executing the Analytics Pipeline locally]({{urlRoot}}/content/services-packages/analytics-pipeline/local)
