# Scale Testing the Analytics Pipeline

In this section we will scale test our analytics pipeline.

1. [Write Events to Endpoint](#1---write-events-to-endpoint)
2. [Verify Events in GCS and BigQuery](#2---verify-events-in-gcs-and-bigquery)

## (1) - Write Events to Endpoint

First let's create a new virtual Python environment & install dependencies.

```bash
# Step out of your current Python 3 virtual environment, if you are in one:
deactivate

# Create a new Python 3 virtual environment:
python3 -m venv venv-scale-test

# Activate virtual environment:
source venv-scale-test/bin/activate

# Upgrade Python's package manager pip:
pip install --upgrade pip

# Install dependencies with pip:
pip install -r ../../services/python/analytics-pipeline/src/requirements/scale-test.txt
```

Second, we will use our [scale test script](../../services/python/analytics-pipeline/src/endpoint/scale-test.py) to write 10k batch files into GSC:

```bash
python ../../services/python/analytics-pipeline/src/endpoint/scale-test.py \
  --gcp-secret-path=[local JSON key path writer] \
  --host=http://analytics.endpoints.[your project id].cloud.goog/ \
  --api-key=[your gcp api key] \
  --bucket-name=[your project id]-analytics \
  --scale-test-name=scale-test \
  --event-category=function \
  --analytics-environment=testing \
  --pool-size=30 \
  --n=10000
```

After the script finishes, copy the **[scale test name]**, **[event ds]** & **[event time]** from the terminal output.

## (2) - Verify Events in GCS and BigQuery

_Tip - You might need to still use a Python 2.7+ interpreter with the gcloud cli for the below commands to work!_

### (2.1) - In GCS

Verify whether our events arrived successfully in GCS:

```bash
QUERY="
SELECT
  a.n,
  COUNT(*) AS freq,
  a.n * COUNT(*) AS total_no_events
FROM
    (
    SELECT
      batchId,
      COUNT(*) AS n
    FROM table_test
    WHERE eventType = '[scale test name]'
    GROUP BY 1
    ) a
GROUP BY 1
;
"
bq query \
  --location=EU \
  --use_legacy_sql=false \
  --external_table_definition=table_test::batchId:STRING,eventType:STRING@NEWLINE_DELIMITED_JSON=gs://[your project id]-analytics/data_type=json/analytics_environment=testing/event_category=function/event_ds=[event ds]/event_time=[event time]/[scale test name]/\* \
  $QUERY

# +---+-------+------------------+
# | n | freq  | total_no_events  |
# +---+-------+------------------+
# | 4 | 10000 |            40000 |
# +---+-------+------------------+
```

### (2.2) - In BigQuery

Next we verify whether our analytics Cloud Function successfully forwarded these events into native BigQuery storage:

```bash
QUERY="
SELECT
  a.n,
  COUNT(*) AS freq,
  a.n * COUNT(*) AS total_no_events
FROM
    (
    SELECT
      batch_id,
      COUNT(*) AS n
    FROM events.events_function
    WHERE event_type = '[scale test name]'
    GROUP BY 1
    ) a
GROUP BY 1
;
"
bq query --use_legacy_sql=false $QUERY

# +---+-------+------------------+
# | n | freq  | total_no_events  |
# +---+-------+------------------+
# | 4 | 10000 |            40000 |
# +---+-------+------------------+
```
