
from common.functions import get_dict_value, cast_to_unix_timestamp, format_event_list, \
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
        ('logs', f'native_events_{os.environ["ENVIRONMENT"]}', 'event_ds'),
        ('logs', f'native_events_debug_{os.environ["ENVIRONMENT"]}', 'event_ds'),
        ('logs', f'dataflow_backfill_{os.environ["ENVIRONMENT"]}', 'event_ds'),
        ('native', f'events_general_{os.environ["ENVIRONMENT"]}', 'event_timestamp')]

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
                    d['analytics_environment'] = get_dict_value(event, 'analyticsEnvironment', 'analytics_environment')
                    d['event_environment'] = get_dict_value(event, 'eventEnvironment', 'event_environment')
                    d['event_source'] = get_dict_value(event, 'eventSource', 'event_source')
                    d['session_id'] = get_dict_value(event, 'sessionId', 'session_id')
                    d['version_id'] = get_dict_value(event, 'versionId', 'version_id')
                    d['batch_id'] = get_dict_value(event, 'batchId', 'batch_id')
                    d['event_id'] = get_dict_value(event, 'eventId', 'event_id')
                    d['event_index'] = get_dict_value(event, 'eventIndex', 'event_index')
                    d['event_class'] = get_dict_value(event, 'eventClass', 'event_class')
                    d['event_type'] = get_dict_value(event, 'eventType', 'event_type')
                    d['player_id'] = get_dict_value(event, 'playerId', 'player_id')
                    d['event_timestamp'] = cast_to_unix_timestamp(get_dict_value(event, 'eventTimestamp', 'event_timestamp'), ['%Y-%m-%dT%H:%M:%SZ', '%Y-%m-%d %H:%M:%S %Z'])
                    d['received_timestamp'] = get_dict_value(event, 'receivedTimestamp', 'received_timestamp')  # This value was set by our endpoint, so we already know it is in unixtime
                    # Augment:
                    d['inserted_timestamp'] = time.time()
                    d['job_name'] = os.environ['FUNCTION_NAME']
                    # Sanitize:
                    d['event_attributes'] = get_dict_value(event, 'eventAttributes', 'event_attributes')
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
