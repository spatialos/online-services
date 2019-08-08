# Python 3.6.5

# python src/p1-gcs-to-bq-backfill.py \
#  --execution-environment=DataflowRunner \
#  --local-sa-key=[local JSON key path for Dataflow] \
#  --bucket-name=[your project id]-analytics \
#  --topic=cloud-function-gcs-to-bq-topic \
#  --gcp=[your project id] \
#  --location=[your analytics bucket location]

from __future__ import absolute_import
import apache_beam as beam

from apache_beam.options.pipeline_options import StandardOptions
from apache_beam.options.pipeline_options import PipelineOptions
from apache_beam.options.pipeline_options import SetupOptions

from common.bigquery import source_bigquery_assets, generate_bigquery_assets, generate_backfill_query
from common.functions import generate_gcs_file_list, convert_list_to_sql_tuple, parse_gspath, parse_argument
from common.classes import GetGcsFileList, WriteToPubSub

from google.cloud import bigquery
import argparse
import hashlib
import time
import sys

parser = argparse.ArgumentParser()
parser.add_argument('--execution-environment', dest='execution_environment', default='DataflowRunner')
parser.add_argument('--setup-file', dest='setup_file', default='src/setup.py')
parser.add_argument('--local-sa-key', dest='local_sa_key', required=True)
parser.add_argument('--topic', default='cloud-function-gcs-to-bq-topic')
parser.add_argument('--location', required=True) # {EU|US}
parser.add_argument('--gcp', required=True)

# gs://{bucket-name}/data_type={json|unknown}/analytics_environment={testing|development|staging|production|live}/event_category={!function}/event_ds={yyyy-mm-dd}/event_time={0-8|8-16|16-24}/[{scale-test-name}]
parser.add_argument('--bucket-name', dest='bucket_name', required=True)
parser.add_argument('--analytics-environment', dest='analytics_environment', default='all')  # {testing|development|staging|production|live}
parser.add_argument('--event-category', dest='event_category', required=True)
parser.add_argument('--event-ds-start', dest='event_ds_start', default='2019-01-01')
parser.add_argument('--event-ds-stop', dest='event_ds_stop', default='2020-12-31')
parser.add_argument('--event-time', dest='event_time', default='all')  # {0-8|8-16|16-24}
parser.add_argument('--scale-test-name', dest='scale_test_name', default='')

args = parser.parse_args()

if args.event_ds_start > args.event_ds_stop:
    print('Error: ds_start cannot be later than ds_stop!')
    sys.exit(1)

if args.topic == 'cloud-function-gcs-to-bq-topic':
    method = 'function'
else:
    method = 'unknown'

environment_list, environment_name = parse_argument(args.analytics_environment, ['testing', 'development', 'staging', 'production', 'live'], 'environments')
time_part_list, time_part_name = parse_argument(args.event_time, ['0-8', '8-16', '16-24'], 'time-parts')


def run():

    client_bq = bigquery.Client.from_service_account_json(args.local_sa_key, location=args.location)
    bigquery_asset_list = [
      ('logs', 'events_logs_function_native', 'event_ds'),
      ('logs', 'events_debug_function_native', 'event_ds'),
      ('logs', 'events_logs_dataflow_backfill', 'event_ds'),
      ('events', 'events_function_native', 'event_timestamp')]
    try:
        source_bigquery_assets(client_bq, bigquery_asset_list)
    except Exception:
        generate_bigquery_assets(client_bq, bigquery_asset_list)

    # https://github.com/apache/beam/blob/master/sdks/python/apache_beam/options/pipeline_options.py
    po = PipelineOptions()
    job_name = 'p1-gcs-to-bq-{method}-backfill-{environment_name}-{event_category}-{event_ds_start}-to-{event_ds_stop}-{event_time}-{ts}'.format(
      method=method, environment_name=environment_name, event_category=args.event_category, event_ds_start=args.event_ds_start, event_ds_stop=args.event_ds_stop, event_time=time_part_name, ts=str(int(time.time())))
    # https://cloud.google.com/dataflow/docs/guides/specifying-exec-params
    pipeline_options = po.from_dictionary({
      'project': args.gcp,
      'staging_location': 'gs://{bucket_name}/data_type=dataflow/batch/staging/{job_name}/'.format(bucket_name=args.bucket_name, job_name=job_name),
      'temp_location': 'gs://{bucket_name}/data_type=dataflow/batch/temp/{job_name}/'.format(bucket_name=args.bucket_name, job_name=job_name),
      'runner': args.execution_environment,  # {DirectRunner, DataflowRunner}
      'setup_file': args.setup_file,
      'service_account_email': 'dataflow-batch@{gcp_project_id}.iam.gserviceaccount.com'.format(gcp_project_id=args.gcp),
      'job_name': job_name
      })
    pipeline_options.view_as(SetupOptions).save_main_session = True

    p1 = beam.Pipeline(options=pipeline_options)
    fileListGcs = (p1 | 'CreateGcsIterators' >> beam.Create(list(generate_gcs_file_list(args.event_ds_start, args.event_ds_stop, args.bucket_name, environment_list, args.event_category, time_part_list, args.scale_test_name)))
                      | 'GetGcsFileList' >> beam.ParDo(GetGcsFileList())
                      | 'GcsListPairWithOne' >> beam.Map(lambda x: (x, 1)))

    fileListBq = (p1 | 'ParseBqFileList' >> beam.io.Read(beam.io.BigQuerySource(
                        # "What is already in BQ?"
                        query=generate_backfill_query(args.gcp, method, args.event_ds_start, args.event_ds_stop, convert_list_to_sql_tuple(time_part_list), convert_list_to_sql_tuple(environment_list), args.event_category, args.scale_test_name),
                        use_standard_sql=True))
                     | 'BqListPairWithOne' >> beam.Map(lambda x: (x['gspath'], 1)))


    parseList = ({'fileListGcs': fileListGcs, 'fileListBq': fileListBq}
                 | 'CoGroupByKey' >> beam.CoGroupByKey()
                 | 'UnionMinusIntersect' >> beam.Filter(lambda x: (len(x[1]['fileListGcs']) == 1 and len(x[1]['fileListBq']) == 0))
                 | 'ExtractKeysParseList' >> beam.Map(lambda x: x[0]))

    # Write to BigQuery
    logsList = (parseList | 'AddParseInitiatedInfo' >> beam.Map(lambda gspath: {'job_name': job_name,
                                                                                'processed_timestamp': time.time(),
                                                                                'batch_id': hashlib.md5(gspath.encode('utf-8')).hexdigest(),
                                                                                'analytics_environment': parse_gspath(gspath, 'analytics_environment='),
                                                                                'event_category': parse_gspath(gspath, 'event_category='),
                                                                                'event_ds': parse_gspath(gspath, 'event_ds='),
                                                                                'event_time': parse_gspath(gspath, 'event_time='),
                                                                                'event': 'parse_initiated',
                                                                                'gspath': gspath})
                          | 'WriteParseInitiated' >> beam.io.WriteToBigQuery(table='events_logs_dataflow_backfill',
                                                                             dataset='logs',
                                                                             project=args.gcp,
                                                                             method='FILE_LOADS',
                                                                             create_disposition=beam.io.gcp.bigquery.BigQueryDisposition.CREATE_IF_NEEDED,
                                                                             write_disposition=beam.io.gcp.bigquery.BigQueryDisposition.WRITE_APPEND,
                                                                             insert_retry_strategy=beam.io.gcp.bigquery_tools.RetryStrategy.RETRY_ON_TRANSIENT_ERROR,
                                                                             schema='job_name:STRING,processed_timestamp:TIMESTAMP,batch_id:STRING,analytics_environment:STRING,event_category:STRING,event_ds:DATE,event_time:STRING,event:STRING,gspath:STRING'))

    # Write to Pub/Sub
    PDone = (parseList | 'DumpParseListPubSub' >> beam.io.WriteToText('gs://{bucket_name}/data_type=dataflow/batch/output/{job_name}/parselist'.format(bucket_name=args.bucket_name, job_name=job_name))
                       | 'WriteToPubSub' >> beam.ParDo(WriteToPubSub(), job_name, args.topic, args.gcp, args.bucket_name))


    p1.run().wait_until_finish()
    return job_name


if __name__ == '__main__':
    job_name = run()
    print('Stream backfill job finished: {job_name}'.format(job_name=job_name))
