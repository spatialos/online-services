from __future__ import absolute_import
import apache_beam as beam


class GetGcsFileList(beam.DoFn):

    """ A custom Beam ParDo to generate a list of files that are present in
    Google Cloud Storage (GCS). It takes path prefixes as elements (~ arguments).
    """

    def process(self, element):
        from apache_beam.io.gcp import gcsio

        prefix = element
        dict_file_path_size = gcsio.GcsIO().list_prefix(prefix)
        file_path_list = dict_file_path_size.keys()

        for file_path in file_path_list:
            yield file_path


class WriteToPubSub(beam.DoFn):

    """ A custom Beam ParDo to notify a Pub/Sub Topic about the existence of files
    in Google Cloud Storage (GCS).

    It first reads GCS URI strings from a fileList stored in GCS, which should have happened in a
    prior step. It then parses these GCS URIs by extracting the file name & bucket name, which it subsequently
    uses as the payload for a Pub/Sub message to a particular Pub/Sub Topic.
    """

    def process(self, element, job_name, topic, gcp, gcs_bucket):
        from apache_beam.io.gcp import gcsio
        from google.cloud import pubsub_v1

        gcs = gcsio.GcsIO()
        prefix = 'gs://{gcs_bucket}/data_type=dataflow/batch/output/{job_name}/parselist'.format(gcs_bucket=gcs_bucket, job_name=job_name)
        dict_file_path_size = gcs.list_prefix(prefix)
        file_path_list = dict_file_path_size.keys()

        # https://cloud.google.com/pubsub/docs/publisher#pubsub-publish-message-python
        client_ps = pubsub_v1.PublisherClient(
          batch_settings = pubsub_v1.types.BatchSettings(max_messages=1000, max_bytes=5120)
          )
        topic = client_ps.topic_path(gcp, topic)

        for file_path in file_path_list:

            try:
                gcs_uri_list_read = gcs.open(filename=file_path, mode='r').read().decode('utf-8').split('\n')
                # With each **file** written into GCS by beam.io.WriteToText(), a PDone is returned & WriteToPubSub() is triggered,
                # so we first remove this file from GCS to avoid duplicative results:
                gcs_uri_list_delete = gcs.delete(path=file_path)

                # Example GCS URI:
                # gs://your-project-name-analytics/data_type=json/analytics_environment=function/event_category=scale-test/event_ds=2019-06-26/event_time=8-16/f58179a375290599dde17f7c6d546d78/2019-06-26T14:28:32Z-107087

                for gcs_uri in gcs_uri_list_read:
                    uri_prefix_offset = 5
                    gcs_bucket = gcs_uri[uri_prefix_offset:].split('/')[0]
                    name = '/'.join(gcs_uri[uri_prefix_offset:].split('/')[1:])
                    if name and gcs_bucket:
                        data = '{"name":"%s","bucket":"%s"}' % (name, gcs_bucket)
                        future = client_ps.publish(topic, data=data.encode('utf-8'))
                        yield (topic, data)

            except Exception:
                pass
