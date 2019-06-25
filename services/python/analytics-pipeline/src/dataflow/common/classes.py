from __future__ import absolute_import
import apache_beam as beam

class getGcsFileList(beam.DoFn):

    def process(self, element):
        from apache_beam.io.gcp import gcsio

        prefix = element
        file_size_dict = gcsio.GcsIO().list_prefix(prefix)
        file_list = file_size_dict.keys()

        for i in file_list:
            yield i

class WriteToPubSub(beam.DoFn):

    def process(self, element, job_name, topic, gcp, gcs_bucket):
        from apache_beam.io.gcp import gcsio
        from google.cloud import pubsub_v1

        gcs = gcsio.GcsIO()
        prefix = 'gs://{gcs_bucket}/data_type=dataflow/batch/output/{job_name}/parselist'.format(gcs_bucket = gcs_bucket, job_name = job_name)
        file_size_dict = gcs.list_prefix(prefix)
        file_list = file_size_dict.keys()

        # https://cloud.google.com/pubsub/docs/publisher#pubsub-publish-message-python
        client_ps = pubsub_v1.PublisherClient(
          batch_settings = pubsub_v1.types.BatchSettings(max_messages = 1000, max_bytes = 5120)
          )
        topic = client_ps.topic_path(gcp, topic)

        for i in file_list:
            gcs_uri_list_read = gcs.open(filename = i, mode = 'r').read().decode('utf-8').split('\n')
            # With each **file** written into GCS by beam.io.WriteToText(), a PDone is returned & WriteToPubSub() is triggered!
            gcs_uri_list_delete = gcs.delete(path = i)

            for gcs_uri in gcs_uri_list_read:
                gcs_bucket = gcs_uri[5:].split('/')[0]
                name = '/'.join(gcs_uri[5:].split('/')[1:])
                if name != "" and gcs_bucket != "":
                    data = '{"name":"%s","bucket":"%s"}' % (name, gcs_bucket)
                    future = client_ps.publish(topic, data = data.encode('utf-8'))
                    yield (topic, data)
