# -*- coding: utf-8 -*-
# Python 3.6.5

from google.cloud import bigquery, storage
import hashlib
import base64
import json
import time
import os

from common.functions import try_parse_json, parse_dict_key, parse_gcs_uri, cast_to_unix_timestamp, cast_to_string
from common.bigquery import source_bigquery_assets, generate_bigquery_assets

# Cloud Function acts as the service account named function-gcs-to-bq@[your project id].iam.gserviceaccount.com
client_gcs, client_bq = storage.Client(), bigquery.Client(location=os.environ['LOCATION'])


def format_event_list(event_list, job_name, gspath):
    new_list = [
      {'job_name': job_name,
       'processed_timestamp': time.time(),
       'batch_id': hashlib.md5(gspath.encode('utf-8')).hexdigest(),
       'analytics_environment': parse_gcs_uri(gspath, 'analytics_environment='),
       'event_category': parse_gcs_uri(gspath, 'event_category='),
       'event_ds': parse_gcs_uri(gspath, 'event_ds='),
       'event_time': parse_gcs_uri(gspath, 'event_time='),
       'event': cast_to_string(event),
       'file_path': gspath} for event in event_list]
    return new_list


def ingest_into_native_bigquery_storage(data, context):

    """ This is the primary function invoked whenever the Cloud Function is triggered.
    It parses the Pub/Sub notification that triggered it by extracting the location of the
    file in Google Cloud Storage (GCS). It subsequently downloads the contents of this file
    from GCS, sanitizes & augments the events within it & finally writes them into native BigQuery storage.
    """

    # Source required datasets & tables:
    bigquery_asset_list = [
      ('logs', 'events_logs_function_native', 'event_ds'),
      ('logs', 'events_debug_function_native', 'event_ds'),
      ('logs', 'events_logs_dataflow_backfill', 'event_ds'),
      ('events', 'events_function_native', 'event_timestamp')]

    try:
        table_logs, table_debug, table_dataflow, table_function = source_bigquery_assets(client_bq, bigquery_asset_list)
    except Exception:
        table_logs, table_debug, table_dataflow, table_function = generate_bigquery_assets(client_bq, bigquery_asset_list)

    # Parse payload:
    payload = json.loads(base64.b64decode(data['data']).decode('utf-8'))
    bucket_name, object_location = payload['bucket'], payload['name']
    gspath = 'gs://{bucket_name}/{object_location}'.format(bucket_name=bucket_name, object_location=object_location)

    # Write log to events_logs_function:
    errors = client_bq.insert_rows(table_logs, format_event_list(['parse_initiated'], os.environ['FUNCTION_NAME'], gspath))
    if errors:
        print('Errors while inserting logs: {errors}'.format(errors=str(errors)))

    # Get file from GCS:
    bucket = client_gcs.get_bucket(bucket_name)
    success, events_batch = try_parse_json(bucket.get_blob(object_location).download_as_string().decode('utf8'))

    # Parse list:
    if success:
        events_batch_function, events_batch_debug = [], []
        for event in events_batch:
            d = {}
            # Verify that the event has eventClass set to something:
            d['event_class'] = parse_dict_key(event, 'eventClass', 'event_class')
            if d['event_class'] is not None:
                # Sanitize:
                d['analytics_environment'] = parse_dict_key(event, 'analyticsEnvironment', 'analytics_environment')
                d['batch_id'] = parse_dict_key(event, 'batchId', 'batch_id')
                d['event_id'] = parse_dict_key(event, 'eventId', 'event_id')
                d['event_index'] = parse_dict_key(event, 'eventIndex', 'event_index')
                d['event_source'] = parse_dict_key(event, 'eventSource', 'event_source')
                d['event_type'] = parse_dict_key(event, 'eventType', 'event_type')
                d['session_id'] = parse_dict_key(event, 'sessionId', 'session_id')
                d['version_id'] = parse_dict_key(event, 'versionId', 'version_id')
                d['event_environment'] = parse_dict_key(event, 'eventEnvironment', 'event_environment')
                d['event_timestamp'] = cast_to_unix_timestamp(parse_dict_key(event, 'eventTimestamp', 'event_timestamp'), ['%Y-%m-%dT%H:%M:%SZ', '%Y-%m-%d %H:%M:%S %Z'])
                d['received_timestamp'] = cast_to_unix_timestamp(parse_dict_key(event, 'receivedTimestamp', 'received_timestamp'), ['%Y-%m-%dT%H:%M:%SZ', '%Y-%m-%d %H:%M:%S %Z'])
                # Augment:
                d['inserted_timestamp'] = time.time()
                d['job_name'] = os.environ['FUNCTION_NAME']
                # Sanitize:
                d['event_attributes'] = parse_dict_key(event, 'eventAttributes', 'event_attributes')
                events_batch_function.append(d)
            else:
                events_batch_debug.append(event)

        if len(events_batch_function) > 0:
            # Write session JSON to events_function:
            errors = client_bq.insert_rows(table_function, events_batch_function)
            if errors:
                print('Errors while inserting events: {errors}'.format(errors=str(errors)))

        if len(events_batch_debug) > 0:
            # Write non-session JSON to events_debug_function:
            errors = client_bq.insert_rows(table_debug, format_event_list(events_batch_debug, os.environ['FUNCTION_NAME'], gspath))
            if errors:
                print('Errors while inserting events: {errors}'.format(errors=str(errors)))

    else:
        # Write non-JSON to debugSink:
        errors = client_bq.insert_rows(table_debug, format_event_list(events_batch, os.environ['FUNCTION_NAME'], gspath))
        if errors:
            print('Errors while inserting debug event: {errors}'.format(errors=str(errors)))
