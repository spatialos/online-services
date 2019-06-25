# -*- coding: utf-8 -*-
# Python 3.6.5

# python src/p1-gcs-to-bq-backfill.py \
#  --execution-environment=DataflowRunner \
#  --local-sa-key=[local JSON key path for Dataflow] \
#  --gcs-bucket=[your project id]-analytics \
#  --topic=cloud-function-gcs-to-bq-topic \
#  --gcp=[your project id]

from __future__ import absolute_import
import apache_beam as beam

from apache_beam.options.pipeline_options import StandardOptions
from apache_beam.options.pipeline_options import PipelineOptions
from apache_beam.options.pipeline_options import SetupOptions

from common.storage import datesGenerator, gcsFileListGenerator
from common.bigquery import provisionBigQuery, queryGenerator
from common.classes import getGcsFileList, WriteToPubSub

import argparse

parser = argparse.ArgumentParser()
parser.add_argument('--execution-environment', dest = 'execution_environment', default = 'DataflowRunner')
parser.add_argument('--setup-file', dest = 'setup_file', default = 'src/setup.py')
parser.add_argument('--local-sa-key', dest = 'local_sa_key', required = True)
parser.add_argument('--topic', default = 'cloud-function-gcs-to-bq-topic')
parser.add_argument('--gcp', required = True)

# gs://{gcs-bucket}/data_type={json|unknown}/analytics_environment={testing|development|staging|production|live}/event_category={!function}/event_ds={yyyy-mm-dd}/event_time={0-8|8-16|16-24}/[{scale-test-name}]
parser.add_argument('--gcs-bucket', dest = 'gcs_bucket', required = True)
parser.add_argument('--analytics-environment', dest = 'analytics_environment', default = 'all') # {testing|development|staging|production|live}
parser.add_argument('--event-category', dest = 'event_category', required = True)
parser.add_argument('--event-ds-start', dest = 'event_ds_start', default = '2019-01-01')
parser.add_argument('--event-ds-stop', dest = 'event_ds_stop', default = '2020-12-31')
parser.add_argument('--event-time', dest = 'event_time', default = 'all') # {0-8|8-16|16-24}
parser.add_argument('--scale-test-name', dest = 'scale_test_name', default = '')

args = parser.parse_args()

if args.event_ds_start > args.event_ds_stop:
    print('Error: ds_start cannot be later than ds_stop!')
    sys.exit()

if args.topic == 'cloud-function-gcs-to-bq-topic':
    method = 'function'
else:
    method = 'unknown'

def run():
    from common.parser import pathParser, envParser, timeParser
    from google.cloud import bigquery
    import datetime
    import hashlib
    import time
    import sys

    list_env, name_env = envParser(args.analytics_environment)
    list_time_part, name_time = timeParser(args.event_time)

    bq_success = provisionBigQuery(bigquery.Client.from_service_account_json(args.local_sa_key), method, True)
    if not bq_success:
        print('Failed to provision required BigQuery resources!')
        sys.exit()

    # https://github.com/apache/beam/blob/master/sdks/python/apache_beam/options/pipeline_options.py
    po = PipelineOptions()
    job_name = 'p1-gcs-to-bq-{method}-backfill-{name_env}-{event_category}-{event_ds_start}-to-{event_ds_stop}-{event_time}-{ts}'.format(
      method = method, name_env = name_env, event_category = args.event_category, event_ds_start = args.event_ds_start, event_ds_stop = args.event_ds_stop, event_time = name_time, ts = str(int(time.time())))
    pipeline_options = po.from_dictionary({
      'project': args.gcp,
      'staging_location': 'gs://{gcs_bucket}/data_type=dataflow/batch/staging/{job_name}/'.format(gcs_bucket = args.gcs_bucket, job_name = job_name),
      'temp_location': 'gs://{gcs_bucket}/data_type=dataflow/batch/temp/{job_name}/'.format(gcs_bucket = args.gcs_bucket, job_name = job_name),
      'runner': args.execution_environment, # {DirectRunner, DataflowRunner}
      'setup_file': args.setup_file,
      'service_account_email': 'dataflow-batch@{gcp_project_id}.iam.gserviceaccount.com'.format(gcp_project_id = args.gcp),
      'job_name': job_name
      })
    pipeline_options.view_as(SetupOptions).save_main_session = True

    p1 = beam.Pipeline(options = pipeline_options)
    fileListGcs = (p1 | 'createGcsIterators' >> beam.Create(list(gcsFileListGenerator(datesGenerator, args.event_ds_start, args.event_ds_stop, args.gcs_bucket, list_env, args.event_category, list_time_part, args.scale_test_name)))
                      | 'getGcsFileList' >> beam.ParDo(getGcsFileList())
                      | 'GcsListPairWithOne' >> beam.Map(lambda x: (x, 1)))

    # fileListGcs | 'dumpGCSFileList' >> beam.io.WriteToText('gs://{gcs_bucket}/data_type=dataflow/batch/output/{job_name}/0_fileListGcs'.format(gcs_bucket = args.gcs_bucket, job_name = job_name)) # Cloud-Debug [when using DataflowRunner]
    # fileListGcs | 'dumpGCSFileList' >> beam.io.WriteToText('local_debug/{job_name}/0_fileListGcs'.format(job_name = job_name)) # Local-Debug [when using DirectRunner]

    fileListBq = (p1 | 'parseBqFileList' >> beam.io.Read(beam.io.BigQuerySource(
                        # "What is already in BQ?"
                        query = queryGenerator(args.gcp, method, args.event_ds_start, args.event_ds_stop, list_time_part, list_env, args.event_category, args.scale_test_name),
                        use_standard_sql = True))
                     | 'bqListPairWithOne' >> beam.Map(lambda x: (x['file_path'], 1)))

    # fileListBq | 'dumpBQFileList' >> beam.io.WriteToText('gs://{gcs_bucket}/data_type=dataflow/batch/output/{job_name}/1_fileListBq'.format(gcs_bucket = args.gcs_bucket, job_name = job_name)) # Cloud-Debug [when using DataflowRunner]
    # fileListBq | 'dumpBQFileList' >> beam.io.WriteToText('local_debug/{job_name}/1_fileListBq'.format(job_name = job_name)) # Local-Debug [when using DirectRunner]

    parseList = ({'fileListGcs': fileListGcs, 'fileListBq': fileListBq}
                  | 'CoGroupByKey' >> beam.CoGroupByKey()
                  | 'unionMinusIntersect' >> beam.Filter(lambda x: (len(x[1]['fileListGcs']) == 1 and len(x[1]['fileListBq']) == 0))
                  | 'extractKeysParseList' >> beam.Map(lambda x: x[0]))

    # parseList | 'dumpParseList' >> beam.io.WriteToText('gs://{gcs_bucket}/data_type=dataflow/batch/output/{job_name}/2_parseList'.format(gcs_bucket = args.gcs_bucket, job_name = job_name)) # Cloud-Debug [when using DataflowRunner]
    # parseList | 'dumpParseList' >> beam.io.WriteToText('local_debug/{job_name}/2_parseList'.format(job_name = job_name)) # Local-Debug [when using DirectRunner]

    # Write to BigQuery
    logsList = (parseList | 'addParseInitiatedInfo' >> beam.Map(lambda x: {'job_name': job_name, 'processed_timestamp': time.time(), 'batch_id': hashlib.md5('/'.join(x.split('/')[-2:]).encode('utf-8')).hexdigest(), 'analytics_environment': pathParser(x, 'analytics_environment='),
                                                                           'event_category': pathParser(x, 'event_category='), 'event_ds': pathParser(x, 'event_ds='), 'event_time': pathParser(x, 'event_time='), 'event': 'parse_initiated', 'file_path': x})
                          | 'writeParseInitiated' >> beam.io.WriteToBigQuery(table = 'events_logs_' + method + '_backfill', dataset = 'logs', project = args.gcp, method = 'FILE_LOADS',
                                                                             create_disposition = beam.io.gcp.bigquery.BigQueryDisposition.CREATE_IF_NEEDED,
                                                                             write_disposition = beam.io.gcp.bigquery.BigQueryDisposition.WRITE_APPEND,
                                                                             insert_retry_strategy = beam.io.gcp.bigquery_tools.RetryStrategy.RETRY_ON_TRANSIENT_ERROR,
                                                                             schema = 'job_name:STRING,processed_timestamp:TIMESTAMP,batch_id:STRING,analytics_environment:STRING,event_category:STRING,event_ds:DATE,event_time:STRING,event:STRING,file_path:STRING'))

    # Write to Pub/Sub
    PDone = (parseList | 'dumpParseListPubSub' >> beam.io.WriteToText('gs://{gcs_bucket}/data_type=dataflow/batch/output/{job_name}/parselist'.format(gcs_bucket = args.gcs_bucket, job_name = job_name))
                       | 'writeToPubSub' >> beam.ParDo(WriteToPubSub(), job_name, args.topic, args.gcp, args.gcs_bucket))

    # PDone | 'dumpPubSubList' >> beam.io.WriteToText('gs://{gcs_bucket}/data_type=dataflow/batch/output/{job_name}/4_pubsubList_{int}'.format(gcs_bucket = args.gcs_bucket, job_name = job_name)) # Cloud-Debug [when using DataflowRunner]
    # PDone | 'dumpParseInitiatedList' >> beam.io.WriteToText('local_debug/{job_name}/4_pubsubList_{int}'.format(job_name = job_name)) # Local-Debug [when using DirectRunner]

    p1.run().wait_until_finish()
    return job_name

if __name__ == '__main__':
  job_name = run()
  print('Stream backfill job finished: {job_name}'.format(job_name = job_name))
