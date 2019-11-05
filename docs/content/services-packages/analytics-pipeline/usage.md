# Analytics Pipeline: usage
<%(TOC)%>

All Online Services have been [instrumented](https://en.wikipedia.org/wiki/Instrumentation_(computer_programming)) out-of-the-box. This means that whenever you deploy the Analytics Pipeline, you automatically capture analytics events originating from these services and can query them with BigQuery in your Google project.

You can however decide to forward more events to the Analytics Pipeline as well, for instance those originating from within your game(s).

## Using the endpoint

To send more data to the Analytics Pipeline, you can make simple `POST` requests with a JSON payload to your endpoint. We recommend the payload to be a JSON list of approximately 100 events.

The endpoint URL takes six parameters:

| Parameter               | Required/Optional    | Description |
|-------------------------|----------|-------------|
| `key`                   | Required | Must be tied to your Google project ([info](https://cloud.google.com/endpoints/docs/openapi/get-started-kubernetes#create_an_api_key_and_set_an_environment_variable)). |
| `analytics_environment` | Optional  | The environment, for example {`testing`, `development` (default), `staging`, `production`}. |
| `event_category`        | Optional   | Defaults to `cold`. See [this section]({{urlRoot}}/content/services-packages/analytics-pipeline/deploy#the-event_category-url-parameter) for more information about this parameter. |
| `event_ds`              | Optional | Defaults to the current UTC date in YYYY-MM-DD format. If needed, you can specify a date that overrides this. |
| `event_time`            | Optional | Defaults to the current UTC time period. If needed, you can set a time period that overrides this, must be one of {`0-8`, `8-16`, `16-24`}. |
| `session_id`            | Optional | Should be set, otherwise defaults to `session-id-not-available`. |

These parameters (except for `key`) play a part in how the data ends up in the GCS bucket:

> gs://[[your Google project id]]-analytics/data_type=[[data_type]]/analytics_environment={{analytics_environment}}/event_category={{event_category}}/event_ds={{event_ds}}/event_time={{event_time}}/{{session_id}}/[[timestamp]]-[[random_alphanum]]

Note that the endpoint:

* automatically determines `[[data_type]]`. It can either be `json` (when valid JSON is `POST`ed) or `unknown` (otherwise).
* automatically sets the fields `[[timestamp]]` and `[[random_alphanum]]` (a random string to avoid collisions) as well.

### The event_category URL parameter

The `event_category` parameter is particularly important:

* When set to `function`, all data contained in the `POST` request is ingested into native BigQuery storage using [the analytics Cloud Function (`function-gcs-to-bq-.*`)](https://console.cloud.google.com/functions/list) you created, for enhanced query performance. Note that `function` is a completely arbitrary string, which you can easily change in [its Terraform configuration](https://github.com/spatialos/online-services/blob/analytics-docs/services/terraform/module-analytics/pubsub.tf).
* When set to anything else, all data contained in the `POST` request arrives in GCS, but is not by default ingested into native BigQuery storage. You can still access these analytics events with BigQuery by using GCS as an external data source.

## The Event Schema

Each analytics event, which is a JSON dictionary, should adhere to the following JSON schema:
| Root key           | Type    | Description |
|--------------------|---------|-------------|
| `eventEnvironment` | string  | The build configuration that the event was sent from, for example {`debug`, `profile`, `release`}. |
| `eventSource`      | string  | Source of the event, which for in-game events equates to worker type. |
| `sessionId`        | string  | A session identifier, which for in-game events is unique per worker instance session. |
| `versionId`        | string  | Version of the game build or online service. Should naturally sort from oldest to latest. |
| `eventIndex`       | integer | Increments by one with each event per `sessionId`. Allows you to spot missing data. |
| `eventClass`       | string  | A higher order mnemonic classification of the event (for example `session`). |
| `eventType`        | string  | A mnemonic event description (for example `session_start`). |
| `playerId`         | string  | A player's unique identifier, if available. |
| `eventTimestamp`   | float   | The timestamp of the event, in Unix time. |
| `eventAttributes`  | dict    | You can capture anything else relating to this particular event in this attribute as a nested JSON dictionary. |

Keep the following in mind:

* keys should always be camelCase, whereas values should be snake_case whenever they are of the type `string`.
* root keys of the dictionary are always present for any event, except for `playerId`, which can for instance be missing pre-login and post-logout (client-side events) or for certain server-side events.
* you should nest anything that is custom to a particular event within `eventAttributes`.

## Example POST request

A `POST` request to the analytics endpoint that includes invoking the analytics Cloud Function (`event_category=function`) looks like this:

```sh
curl --request POST --header "Content-Type:application/json" --data "[{\"eventEnvironment\":\"testing\",\"eventSource\":\"client\",\"sessionId\":\"f58179a375290599dde17f7c6d546d78\",\"versionId\":\"0.0.1\",\"eventIndex\":0,\"eventClass\":\"docs\",\"eventType\":\"test\",\"playerId\":\"12345678\",\"eventTimestamp\":1562599755,\"eventAttributes\":{\"hello\":\"world\"}},{\"eventEnvironment\":\"testing\",\"eventSource\":\"client\",\"sessionId\":\"f58179a375290599dde17f7c6d546d78\",\"versionId\":\"0.0.1\",\"eventIndex\":1,\"eventClass\":\"docs\",\"eventType\":\"test\",\"playerId\":\"12345678\",\"eventTimestamp\":1562599755,\"eventAttributes\":{\"hello\":\"world\"}}]" "http://analytics.endpoints.{{your_google_project_id}}.cloud.goog:80/v1/event?key={{gcp_api_key}}&analytics_environment={{analytics_environment}}&event_category={{event_category}}&session_id={{session_id}}"

# Successful response:
{"code":200,"destination":{"formatted":"gs://cosmic-abbey-186211-analytics/data_type=json/analytics_environment=testing/event_category=function/event_ds=2019-10-30/event_time=8-16/f58179a375290599dde17f7c6d546d78/2019-10-30T12:09:59Z-NVSNU4.jsonl"}}
```

To test this yourself, replace:

* `{{your_google_project_id}}` and `{{gcp_api_key}}` (created in [step 3.1 of the deploy section]({{urlRoot}}/content/services-packages/analytics-pipeline/deploy#31---store-your-secret)) with your own values
* `{{analytics_environment}}` with `testing`
* `{{event_category}}` with `function`
* `{{session_id}}` with any made up session identifier (such as `f58179a375290599dde17f7c6d546d78`)

Then, navigate to [BigQuery](https://console.cloud.google.com/bigquery) and submit the following queries to check your results:

```sql
-- Querying the GCS bucket directly:
SELECT * FROM events.events_gcs_external LIMIT 100;

-- Checking whether our Cloud Function correctly copied the events over into native BigQuery storage:
SELECT * FROM events.events_function_native LIMIT 100;
```

## Next steps

Some options include:

* [Enabling SSL for Cloud Endpoints](https://cloud.google.com/endpoints/docs/openapi/enabling-ssl)
* [Executing backfills]({{urlRoot}}/content/services-packages/analytics-pipeline/backfill)
* [Executing the Analytics Pipeline locally]({{urlRoot}}/content/services-packages/analytics-pipeline/local)
