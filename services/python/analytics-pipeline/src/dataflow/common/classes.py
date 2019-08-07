from __future__ import absolute_import
from apache_beam.io.gcp import gcsio
from google.cloud import pubsub_v1
import apache_beam as beam
import logging

class GetGcsFileList(beam.DoFn):

    """ A custom Beam ParDo to generate a list of files that are present in
    Google Cloud Storage (GCS). It takes path prefixes as elements (~ arguments).
    """

    def process(self, element):

        gspath_prefix = element
        dict_gspath_size = gcsio.GcsIO().list_prefix(gspath_prefix)
        gspath_list = dict_file_path_size.keys()

        for gspath in gspath_list:
            yield gspath


class WriteToPubSub(beam.DoFn):

    """ A custom Beam ParDo to notify a Pub/Sub Topic about the existence of files
    in Google Cloud Storage (GCS).

    It first reads GCS URI strings from a fileList stored in GCS, which should have happened in a
    prior step. It then parses these GCS URIs by extracting the file name & bucket name, which it subsequently
    uses as the payload for a Pub/Sub message to a particular Pub/Sub Topic.
    """

    def process(self, element, job_name, topic, gcp, gcs_bucket):

        gcs = gcsio.GcsIO()
        prefix = 'gs://{gcs_bucket}/data_type=dataflow/batch/output/{job_name}/parselist'.format(gcs_bucket=gcs_bucket, job_name=job_name)
        dict_file_path_size = gcs.list_prefix(prefix)
        gspath_lists = dict_file_path_size.keys()

        # https://cloud.google.com/pubsub/docs/publisher#pubsub-publish-message-python
        client_ps = pubsub_v1.PublisherClient(
          batch_settings = pubsub_v1.types.BatchSettings(max_messages=1000, max_bytes=5120)
          )
        topic = client_ps.topic_path(gcp, topic)

        for gspath_list in gspath_lists:

            try:
                gspath_list_open = gcs.open(filename=gspath_list, mode='r').read().decode('utf-8').split('\n')
                # With each **file** written into GCS by beam.io.WriteToText(), a PDone is returned & WriteToPubSub() is triggered,
                # so we first remove this file from GCS to avoid duplicative results:
                gcs.delete(path=gspath_list)

                # Example GCS URI:
                # gs://your-project-name-analytics/data_type=json/analytics_environment=function/event_category=scale-test/event_ds=2019-06-26/event_time=8-16/f58179a375290599dde17f7c6d546d78/2019-06-26T14:28:32Z-107087

                for gspath in gspath_list_open:
                    gspath_formatted = gcs_uri.split("gs://", 1).pop().split('/')
                    bucket_name, object_location = gspath_formatted[0], '/'.join(gspath_formatted[1:])
                    if name and gcs_bucket:
                        data = '{"name":"%s","bucket":"%s"}' % (object_location, bucket_name)
                        future = client_ps.publish(topic, data=data.encode('utf-8'))
                        yield (topic, data)

            except Exception as e:
                logging.info('Could not parse {gspath_list}: {e}'.format(gspath_list=gspath_list, e=e))
                continue
