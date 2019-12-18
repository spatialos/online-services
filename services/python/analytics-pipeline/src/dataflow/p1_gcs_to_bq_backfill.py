# Python 3.7.1

from __future__ import absolute_import
import apache_beam as beam

from apache_beam.options.pipeline_options import StandardOptions
from apache_beam.options.pipeline_options import PipelineOptions
from apache_beam.options.pipeline_options import SetupOptions

from common.bigquery import source_bigquery_assets, generate_bigquery_assets, generate_backfill_query
from common.functions import parse_none_or_string, generate_gcs_file_list, safe_convert_list_to_sql_tuple, parse_gspath, parse_argument
from common.classes import GetGcsFileList, WriteToPubSub
from google.cloud import bigquery

import argparse
import hashlib
import time
import sys
import os

parser = argparse.ArgumentParser()

parser.add_argument('--execution-environment', dest='execution_environment', default='DataflowRunner')
parser.add_argument('--setup-file', dest='setup_file', default='src/setup.py')
parser.add_argument('--gcp-region', dest='gcp_region', required=True)
parser.add_argument('--environment', required=True)
parser.add_argument('--location', required=True)  # {EU|US}
parser.add_argument('--topic', required=True)
parser.add_argument('--gcp', required=True)

# The following arguments follow along with the gspath:
# gs://{bucket-name}/data_type={jsonl|unknown}/event_schema={improbable|playfab}/event_category={!native}/event_environment={testing|staging|production}/event_ds={yyyy-mm-dd}/event_time={0-8|8-16|16-24}/[{scale-test-name}]

parser.add_argument('--bucket-name', dest='bucket_name', required=True)
parser.add_argument('--event-schema', dest='event_schema', required=True)  # {improbable|playfab}
parser.add_argument('--event-environment', dest='event_environment', required=True)
parser.add_argument('--event-category', dest='event_category', type=parse_none_or_string, default='all')
parser.add_argument('--event-ds-start', dest='event_ds_start', type=parse_none_or_string, default='2019-01-01')
parser.add_argument('--event-ds-stop', dest='event_ds_stop', type=parse_none_or_string, default='2020-12-31')
parser.add_argument('--event-time', dest='event_time', type=parse_none_or_string, default='all')  # {0-8|8-16|16-24}
parser.add_argument('--scale-test-name', dest='scale_test_name', type=parse_none_or_string, default=None)

args = parser.parse_args()

if None not in [args.event_ds_start, args.event_ds_stop]:
    if args.event_ds_start > args.event_ds_stop:
        raise Exception('Error: ds_start cannot be later than ds_stop!')

if args.event_schema not in ['improbable', 'playfab']:
    raise Exception(f'Unknown schema passed {args.event_schema}')

time_part_list, time_part_name = parse_argument(args.event_time, ['0-8', '8-16', '16-24'], 'time-parts')
category_list, category_name = parse_argument(args.event_category, ['cold'], 'categories')

def run():

    client_bq = bigquery.Client.from_service_account_json(os.environ['GOOGLE_APPLICATION_CREDENTIALS'], location=args.location)
    bigquery_asset_list = [
        # (dataset, table_name, table_schema, table_partition_column)
        ('logs', f'native_events_{args.environment}', 'logs', 'event_ds'),
        ('logs', f'native_events_debug_{args.environment}', 'logs', 'event_ds'),
        ('logs', f'dataflow_backfill_{args.environment}', 'logs', 'event_ds'),
        ('native', f'events_{args.event_schema}_{args.environment}', args.event_schema, 'event_timestamp')]
    try:
        source_bigquery_assets(client_bq, bigquery_asset_list)
    except Exception:
        generate_bigquery_assets(client_bq, bigquery_asset_list)

    # https://github.com/apache/beam/blob/master/sdks/python/apache_beam/options/pipeline_options.py
    po, event_category = PipelineOptions(), args.event_category.replace('_', '-')
    job_name = f'p1-gcs-to-bq-backfill-{environment_name}-{event_category}-{args.event_ds_start}-to-{args.event_ds_stop}-{time_part_name}-{int(time.time())}'
    # https://cloud.google.com/dataflow/docs/guides/specifying-exec-params
    pipeline_options = po.from_dictionary({
        'project': args.gcp,
        'staging_location': f'gs://{args.bucket_name}/data_type=dataflow/batch/staging/{job_name}/',
        'temp_location': f'gs://{args.bucket_name}/data_type=dataflow/batch/temp/{job_name}/',
        'runner': args.execution_environment,  # {DirectRunner, DataflowRunner}
        'setup_file': args.setup_file,
        'service_account_email': f'dataflow-batch-{args.environment}@{args.gcp}.iam.gserviceaccount.com',
        'job_name': job_name,
        'region': args.gcp_region
        })
    pipeline_options.view_as(SetupOptions).save_main_session = True

    p1 = beam.Pipeline(options=pipeline_options)
    fileListGcs = (p1 | 'CreateGcsIterators' >> beam.Create(list(generate_gcs_file_list(args.bucket_name, args.event_schema, environment_list, category_list, args.event_ds_start, args.event_ds_stop, time_part_list, args.scale_test_name)))
                   | 'GetGcsFileList' >> beam.ParDo(GetGcsFileList())
                   | 'GcsListPairWithOne' >> beam.Map(lambda x: (x, 1)))

    fileListBq = (p1 | 'ParseBqFileList' >> beam.io.Read(beam.io.BigQuerySource(
        # "What is already in BQ?"
        query=generate_backfill_query(
            args.gcp,
            args.environment,
            event_category,
            (safe_convert_list_to_sql_tuple(environment_list), environment_name),
            (safe_convert_list_to_sql_tuple(category_list), category_name),
            args.event_ds_start,
            args.event_ds_stop,
            (safe_convert_list_to_sql_tuple(time_part_list), time_part_name),
            args.scale_test_name),
        use_standard_sql=True))
                  | 'BqListPairWithOne' >> beam.Map(lambda x: (x['gspath'], 1)))


    parseList = ({'fileListGcs': fileListGcs, 'fileListBq': fileListBq}
                 | 'CoGroupByKey' >> beam.CoGroupByKey()
                 | 'UnionMinusIntersect' >> beam.Filter(lambda x: (len(x[1]['fileListGcs']) == 1 and len(x[1]['fileListBq']) == 0))
                 | 'ExtractKeysParseList' >> beam.Map(lambda x: x[0]))

    # Write to BigQuery:
    logsList = (parseList | 'AddParseInitiatedInfo' >> beam.Map(
                lambda gspath: {
                    'job_name': job_name,
                    'processed_timestamp': time.time(),
                    'batch_id': hashlib.md5(gspath.encode('utf-8')).hexdigest(),
                    'event_category': parse_gspath(gspath, 'event_category='),
                    'event_environment': parse_gspath(gspath, 'event_environment='),
                    'event_ds': parse_gspath(gspath, 'event_ds='),
                    'event_time': parse_gspath(gspath, 'event_time='),
                    'event': 'parse_initiated',
                    'gspath': gspath
                    })
                | 'WriteParseInitiated' >> beam.io.WriteToBigQuery(
                    table='events_logs_dataflow_backfill',
                    dataset='logs',
                    project=args.gcp,
                    method='FILE_LOADS',
                    create_disposition=beam.io.gcp.bigquery.BigQueryDisposition.CREATE_IF_NEEDED,
                    write_disposition=beam.io.gcp.bigquery.BigQueryDisposition.WRITE_APPEND,
                    insert_retry_strategy=beam.io.gcp.bigquery_tools.RetryStrategy.RETRY_ON_TRANSIENT_ERROR,
                    schema='job_name:STRING,processed_timestamp:TIMESTAMP,batch_id:STRING,event_environment:STRING,event_category:STRING,event_ds:DATE,event_time:STRING,event:STRING,gspath:STRING'
                    )
                )

    # Write to Pub/Sub:
    PDone = (parseList | 'DumpParseListPubSub' >> beam.io.WriteToText(f'gs://{args.bucket_name}/data_type=dataflow/batch/output/{job_name}/parselist')
            | 'WriteToPubSub' >> beam.ParDo(WriteToPubSub(), job_name, args.topic, args.gcp, args.bucket_name))

    p1.run().wait_until_finish()
    return job_name


if __name__ == '__main__':
    job_name = run()
    print(f'Stream backfill job finished: {job_name}')
