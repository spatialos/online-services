# Analytics Pipeline: overview
<%(TOC)%>

You can use the Analytics Pipeline to capture and persist JSON analytics events that come from other Online Services, games, or any other type of source that is connected to the internet. Unlike the other Online Services, which use C#, the Analytics Pipeline uses Python, because it is highly popular among data professionals, and because it meant that we could use a single language across all elements of the pipeline. The Analytics Pipeline is currently only supported on Google Cloud.

Whereas you can use logs and metrics to ensure your systems are currently operating correctly, you can use analytics to take a longer-term view, draw inferences, and guide decision making.

The fundamental parts of the Analytics Pipeline are:

* a REST API that acts as the start of the pipeline and accepts JSON events, either as standalone dictionaries, or batched up in lists (recommended). The API sanitizes, augments, and neatly stores these events in a Google Cloud Storage (GCS) bucket.
* a GCS bucket, where we persist all analytics events as newline delimited JSON files. We use BigQuery to instantly query data inside GCS using SQL.
* a Google Cloud Function, which you can use to copy events into native BigQuery storage as soon as they arrive in GCS (instead of using GCS as a federated data source), for enhanced query performance. We do not do this by default, but you can easily do so if you want.

You can optionally use the Cloud Dataflow batch backfill script to easily (re-)ingest certain files in your GCS bucket into Native BigQuery storage at any point (running backfills). See the [Backfills]({{urlRoot}}/content/services-packages/analytics-pipeline/backfill) section.
