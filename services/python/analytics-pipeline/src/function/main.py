# -*- coding: utf-8 -*-
# Python 3.6.5

from google.cloud import bigquery, storage
import hashlib
import base64
import json
import time
import os

from common.functions import json_parser, parse_field, path_parser, unix_timestamp_check
from common.bigquery import provision_bigquery

# Function acts as function-gcs-to-bq@[your project id].iam.gserviceaccount.com
client_gcs, client_bq = storage.Client(), bigquery.Client()


def element_cast(element):
    if isinstance(element, dict):
        return json.dumps(element)
    else:
        return element


def event_formatter(list, job_name, gspath):
    new_list = [
      {'job_name': job_name,
       'processed_timestamp': time.time(),
       'batch_id': hashlib.md5('/'.join(gspath.split('/')[-2:]).encode('utf-8')).hexdigest(),
       'analytics_environment': path_parser(gspath, 'analytics_environment='),
       'event_category': path_parser(gspath, 'event_category='),
       'event_ds': path_parser(gspath, 'event_ds='),
       'event_time': path_parser(gspath, 'event_time='),
       'event': element_cast(i),
       'file_path': gspath} for i in list]
    return new_list


def source_bigquery():
    dataset_logs_ref, dataset_events_ref = client_bq.dataset('logs'), client_bq.dataset('events')
    table_logs_ref, table_debug_ref, table_function_ref = dataset_logs_ref.table('events_logs_function'), dataset_logs_ref.table('events_debug_function'), dataset_events_ref.table('events_function')
    return client_bq.get_table(table_logs_ref), client_bq.get_table(table_debug_ref), client_bq.get_table(table_function_ref)


def gcs_to_bigquery(data, context):

    """ This is the primary function invoked whenever the Cloud Function is triggered.
    It parses the Pub/Sub notification that triggered it by extracting the location of the
    file in Google Cloud Storage (GCS). It subsequently downloads the contents of this file
    from GCS, sanitizes & augments the events within it & finally writes them into native BigQuery storage.
    """

    # Source required datasets & tables:
    try:
        table_logs, table_debug, table_function = source_bigquery()
    except Exception:
        success = provision_bigquery(client_bq, 'function')
        if success:
            table_logs, table_debug, table_function = source_bigquery()
        else:
            raise Exception('Could not provision required BigQuery assets!')

    # Parse payload:
    payload = json.loads(base64.b64decode(data['data']).decode('utf-8'))
    bucket_name, file_name = payload['bucket'], payload['name']
    gspath = 'gs://{bucket_name}/{file_name}'.format(
      bucket_name=bucket_name, file_name=file_name)

    # Write log to events_logs_function:
    errors = client_bq.insert_rows(table_logs, event_formatter(['parse_initiated'], os.environ['FUNCTION_NAME'], gspath))
    if errors:
        print('Errors while inserting logs: ' + str(errors))

    # Get file from GCS:
    bucket = client_gcs.get_bucket(bucket_name)
    batch = json_parser(bucket.get_blob(file_name).download_as_string().decode('utf8'))

    # If dict nest in list:
    if isinstance(batch, dict):
        batch = [batch]

    # Parse list:
    if isinstance(batch, list):
        batch_function, batch_debug = [], []
        for event in batch:
            d = {}
            # Sanitize:
            d['event_class'] = parse_field(_dict=event, option1='eventClass', option2='event_class')
            if d['event_class'] is not None:
                d['analytics_environment'] = parse_field(_dict=event, option1='analyticsEnvironment', option2='analytics_environment')
                d['batch_id'] = parse_field(_dict=event, option1='batchId', option2='batch_id')
                d['event_id'] = parse_field(_dict=event, option1='eventId', option2='event_id')
                d['event_index'] = parse_field(_dict=event, option1='eventIndex', option2='event_index')
                d['event_source'] = parse_field(_dict=event, option1='eventSource', option2='event_source')
                d['event_type'] = parse_field(_dict=event, option1='eventType', option2='event_type')
                d['session_id'] = parse_field(_dict=event, option1='sessionId', option2='session_id')
                d['build_version'] = parse_field(_dict=event, option1='buildVersion', option2='build_version')
                d['event_environment'] = parse_field(_dict=event, option1='eventEnvironment', option2='event_environment')
                d['event_timestamp'] = unix_timestamp_check(parse_field(_dict=event, option1='eventTimestamp', option2='event_timestamp'))
                d['received_timestamp'] = unix_timestamp_check(parse_field(_dict=event, option1='receivedTimestamp', option2='received_timestamp'))
                # Augment:
                d['inserted_timestamp'] = time.time()
                d['job_name'] = os.environ['FUNCTION_NAME']
                # Sanitize:
                d['event_attributes'] = parse_field(_dict=event, option1='eventAttributes', option2='event_attributes')
                batch_function.append(d)
            else:
                batch_debug.append(event)

        if len(batch_function) > 0:
            # Write session JSON to events_function:
            errors = client_bq.insert_rows(table_function, batch_function)
            if errors:
                print('Errors while inserting events: ' + str(errors))

        if len(batch_debug) > 0:
            # Write non-session JSON to events_debug_function:
            errors = client_bq.insert_rows(table_debug, event_formatter(batch_debug, os.environ['FUNCTION_NAME'], gspath))
            if errors:
                print('Errors while inserting events: ' + str(errors))

    else:
        # Write non-JSON to debugSink:
        errors = client_bq.insert_rows(table_debug, event_formatter([batch], os.environ['FUNCTION_NAME'], gspath))
        if errors:
            print('Errors while inserting debug event: ' + str(errors))
