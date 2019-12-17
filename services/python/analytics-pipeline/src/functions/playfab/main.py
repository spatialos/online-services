
from common.functions import cast_to_unix_timestamp, format_event_list, \
  gunzip_bytes_obj, generator_split, generator_chunk, generator_load_json
from common.bigquery import source_bigquery_assets, generate_bigquery_assets
from google.cloud import bigquery, storage

import base64
import json
import time
import os

# Cloud Function acts as the service account named function-gcs-to-bq@[your Google project id].iam.gserviceaccount.com
client_gcs, client_bq = storage.Client(), bigquery.Client(location=os.environ['LOCATION'])


def ingest_into_native_bigquery_storage(data, context):

    """ This is the primary function invoked whenever the Cloud Function is triggered.
    It parses the Pub/Sub notification that triggered it by extracting the location of the
    file in Google Cloud Storage (GCS). It subsequently downloads the contents of this file
    from GCS, sanitizes & augments the events within it & finally writes them into native BigQuery storage.
    """

    # Source required datasets & tables:
    bigquery_asset_list = [
        # Schema: (dataset, table_name, partition_column)
        ('logs', f'events_native_{os.environ["ENVIRONMENT"]}', 'event_ds'),
        ('logs', f'events_native_debug_{os.environ["ENVIRONMENT"]}', 'event_ds'),
        ('logs', f'dataflow_backfill_{os.environ["ENVIRONMENT"]}', 'event_ds'),
        ('playfab', f'events_native_{os.environ["ENVIRONMENT"]}', 'event_timestamp')]

    try:
        table_logs, table_debug, _, table_function = source_bigquery_assets(client_bq, bigquery_asset_list)
    except Exception:
        table_logs, table_debug, _, table_function = generate_bigquery_assets(client_bq, bigquery_asset_list)

    # Parse payload:
    payload = json.loads(base64.b64decode(data['data']).decode('utf-8'))
    bucket_name, object_location = payload['bucket'], payload['name']
    gspath = f'gs://{bucket_name}/{object_location}'

    # Write log to events_logs_function:
    malformed, failed_insertion = False, False
    errors = client_bq.insert_rows(table_logs, format_event_list(['parse_initiated'], str, os.environ['FUNCTION_NAME'], gspath))
    if errors:
        print(f'Errors while inserting logs: {str(errors)}')
        failed_insertion = True

    # Get file from GCS:
    bucket = client_gcs.get_bucket(bucket_name)
    try:
        data = bucket.get_blob(object_location).download_as_string().decode('utf8')
    except UnicodeDecodeError:
        print('Automatic decompressive transcoding failed, unzipping content..')
        data = gunzip_bytes_obj(bucket.get_blob(object_location).download_as_string()).decode('utf-8')
    except Exception:
        raise Exception(f'Could not retrieve file gs://{bucket_name}/{object_location} from GCS!')

    # We use generators in order to save memory usage, allowing the Cloud Function to use the smallest capacity template:
    for chunk in generator_chunk(generator_split(data, '\n'), 1000):
        events_batch_function, events_batch_debug = [], []
        for event_tuple in generator_load_json(chunk):
            if event_tuple[0]:
                for event in event_tuple[1]:
                    d = dict()
                    # Sanitize:
                    d['analytics_environment'] = event.get('AnalyticsEnvironment', None)
                    d['playfab_environment'] = event.get('PlayFabEnvironment', None)
                    d['source_type'] = event.get('SourceType', None)
                    d['source'] = event.get('Source', None)
                    d['event_namespace'] = event.get('EventNamespace', None)
                    d['title_id'] = event.get('TitleId', None)
                    d['batch_id'] = event.get('BatchId', None)
                    d['event_id'] = event.get('EventId', None)
                    d['event_name'] = event.get('EventName', None)
                    d['entity_type'] = event.get('EntityType', None)
                    d['entity_id'] = event.get('EntityId', None)
                    # The PlayFab `Timestamp` has 7 microseconds instead of 6, which we must right trim to parse correctly
                    event_timestamp = event.get('Timestamp', None)
                    d['event_timestamp'] = cast_to_unix_timestamp(event_timestamp[:26] if event_timestamp else None, ['%Y-%m-%dT%H:%M:%S.%f'])
                    d['received_timestamp'] = event.get('ReceivedTimestamp', None)  # This value was set by our endpoint, so we already know it is in unixtime
                    # Augment:
                    d['inserted_timestamp'] = time.time()
                    d['job_name'] = os.environ['FUNCTION_NAME']
                    # Sanitize:
                    d['event_attributes'] = event.get('EventAttributes', None)
                    events_batch_function.append(d)
            else:
                events_batch_debug.append(event_tuple[1])

        if len(events_batch_function) > 0:
            # Write JSON to events_function:
            errors = client_bq.insert_rows(table_function, events_batch_function)
            if errors:
                print(f'Errors while inserting events: {str(errors)}')
                failed_insertion = True

        if len(events_batch_debug) > 0:
            # Write non-JSON to events_debug_function:
            errors = client_bq.insert_rows(table_debug, format_event_list(events_batch_debug, str, os.environ['FUNCTION_NAME'], gspath))
            if errors:
                print(f'Errors while inserting debug event: {str(errors)}')
                failed_insertion = True
            malformed = True

    # We only `raise` now because further iterations of the execution loop could have still succeeded:
    if failed_insertion and malformed:
        raise Exception(f'Failed to insert records into BigQuery, inspect logs! Non-JSON data present in gs://{bucket_name}/{object_location}')
    if failed_insertion:
        raise Exception('Failed to insert records into BigQuery, inspect logs!')
    if malformed:
        raise Exception(f'Non-JSON data present in gs://{bucket_name}/{object_location}')

    return 200
