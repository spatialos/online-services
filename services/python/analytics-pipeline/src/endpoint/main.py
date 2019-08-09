# Python 3.6.5

import Crypto.PublicKey.RSA as RSA
import subprocess
import logging
import hashlib
import string
import json
import gzip
import os

from common.functions import try_format_event, get_date_time
from common.classes import CloudStorageURLSigner
from flask import Flask, jsonify, request
from six.moves import http_client
from google.cloud import storage
from random import choices

client_storage = storage.Client.from_service_account_json(os.environ['GOOGLE_SECRET_KEY_JSON_ANALYTICS_GCS_WRITER'])
bucket = client_storage.get_bucket(os.environ['ANALYTICS_BUCKET_NAME'])

try:
    try:
        subprocess.check_call('base64 --decode %s > /tmp/analytics-gcs-writer.p12' % os.environ['GOOGLE_SECRET_KEY_P12_ANALYTICS_GCS_WRITER'], shell=True)
    except Exception:
        subprocess.call('cp %s /tmp/analytics-gcs-writer.p12' % os.environ['GOOGLE_SECRET_KEY_P12_ANALYTICS_GCS_WRITER'], shell=True)

    subprocess.call('openssl pkcs12 -passin pass:notasecret -in /tmp/analytics-gcs-writer.p12 -nodes -nocerts > /tmp/analytics-gcs-writer.pem', shell=True)
    subprocess.call('openssl rsa -in /tmp/analytics-gcs-writer.pem -inform PEM -out /tmp/analytics-gcs-writer.der -outform DER', shell=True)
    key_der = open('/tmp/analytics-gcs-writer.der', 'rb').read()
    private_key = RSA.importKey(key_der)
    signer = CloudStorageURLSigner(private_key, os.environ['GOOGLE_SERVICE_ACCOUNT_EMAIL_ANALYTICS_GCS_WRITER'])

except Exception:
    print("Could not convert .p12 key into DER format! File endpoint `v1/file` not available..")

app = Flask(__name__)

@app.route('/v1/event', methods=['POST'])
def store_event_in_gcs(bucket=bucket, bucket_name=os.environ['ANALYTICS_BUCKET_NAME']):
    try:
        ts_fmt, ds, event_time = get_date_time()

        analytics_environment = request.args.get('analytics_environment', 'development') or 'development'  # (parameter, default_value) or parameter_value_if_none
        event_category = request.args.get('event_category', 'cold') or 'cold'
        event_ds = request.args.get('ds', ds) or ds
        event_time = request.args.get('time', event_time) or event_time
        session_id = request.args.get('session_id', 'session_id_not_available') or 'session_id_not_available'

        object_location_template = 'data_type={data_type}/analytics_environment={analytics_environment}/event_category={event_category}/event_ds={event_ds}/event_time={event_time}/{session_id}/{ts_fmt}-{random}'
        object_location_json, object_location_json_raw, object_location_unknown = [object_location_template.format(data_type=data_type, analytics_environment=analytics_environment,
          event_category=event_category, event_ds=event_ds, event_time=event_time, session_id=session_id, ts_fmt=ts_fmt, random=''.join(choices(string.ascii_uppercase + string.digits, k=6)))
            for data_type in ['json', 'json_raw', 'unknown']]

        try:
            payload = request.get_json(force=True)
            gspath_json = 'gs://{bucket_name}/{object_location}'.format(bucket_name=bucket_name, object_location=object_location_json)
            batch_id_json = hashlib.md5(gspath_json.encode('utf-8')).hexdigest()
            events_formatted, events_raw = [], []

            # If dict nest in list:
            if isinstance(payload, dict):
                payload = [payload]

            # Parse list:
            if isinstance(payload, list):
                for index, event in enumerate(payload):
                    success, tried_event = try_format_event(index, event, batch_id_json, analytics_environment)
                    if success:
                        events_formatted.append(json.dumps(tried_event))
                    else:
                        events_raw.append(json.dumps(tried_event))

            destination = {}

            # Write formatted JSON events:
            if len(events_formatted) > 0:
                blob = bucket.blob('{object_location}.{file_extension}'.format(object_location=object_location_json, file_extension='jsonl'))
                blob.content_encoding = 'gzip'
                blob.upload_from_string(gzip.compress(bytes('\n'.join(events_formatted), encoding='utf-8')), content_type='text/plain; charset=utf-8')
                destination['formatted'] = '{gspath}.{file_extension}'.format(gspath=gspath_json, file_extension='jsonl')

            # Write raw JSON events:
            if len(events_raw) > 0:
                blob = bucket.blob('{object_location}.{file_extension}'.format(object_location=object_location_json_raw, file_extension='jsonl'))
                blob.content_encoding = 'gzip'
                blob.upload_from_string(gzip.compress(bytes('\n'.join(events_raw), encoding='utf-8')), content_type='text/plain; charset=utf-8')
                destination['raw'] = 'gs://{bucket_name}/{object_location}.{file_extension}'.format(bucket_name=bucket_name, object_location=object_location_json_raw, file_extension='jsonl')

            return jsonify({'code': 200, 'destination': destination})

        except Exception:
            payload = request.get_data(as_text=True)
            blob = bucket.blob(object_location_unknown)
            blob.upload_from_string(payload, content_type='text/plain; charset=utf-8')

            return jsonify({'code': 200, 'destination': {'unknown': 'gs://{bucket_name}/{object_location}'.format(bucket_name=bucket_name, object_location=object_location_unknown)}})

    except Exception as e:
        return jsonify({'message': 'Exception: {e}'.format(e=type(e).__name__), 'args': e.args})


@app.route('/v1/file', methods=['POST'])
def return_signed_url_gcs():
    try:
        ts_fmt, ds, event_time = get_date_time()

        analytics_environment = request.args.get('analytics_environment', 'development') or 'development'  # (parameter, default_value) or parameter_value_if_none
        event_category = request.args.get('event_category', 'unknown') or 'unknown'
        event_ds = request.args.get('ds', ds) or ds
        event_time = request.args.get('time', event_time) or event_time
        file_parent = request.args.get('file_parent', 'unknown') or 'unknown'
        file_child = request.args.get('file_child', 'unknown') or 'unknown'

        payload = request.get_json(force=True)

        object_location_template = 'data_type={data_type}/analytics_environment={analytics_environment}/event_category={event_category}/event_ds={event_ds}/event_time={event_time}/{file_parent}/{file_child}-{random}'
        object_location = object_location_template.format(data_type='file', analytics_environment=analytics_environment, event_category=event_category,
          event_ds=event_ds, event_time=event_time, file_parent=file_parent, file_child=file_child, random=''.join(choices(string.ascii_uppercase + string.digits, k=6)))

        file_path = '/{bucket_name}/{object_location}'.format(bucket_name=os.environ['ANALYTICS_BUCKET_NAME'], object_location=object_location)
        signed = signer.put(path=file_path, content_type=payload['content_type'], md5_digest=payload['md5_digest'])
        return jsonify(signed)

    except Exception as e:
        return jsonify({'message': 'Exception: {e}'.format(e=type(e).__name__), 'args': e.args})


@app.errorhandler(http_client.INTERNAL_SERVER_ERROR)
def unexpected_error(e):
    """Handle exceptions by returning swagger-compliant json."""
    logging.exception('An error occured while processing the request.')
    response = jsonify({
        'code': http_client.INTERNAL_SERVER_ERROR,
        'message': 'Exception: {}'.format(e)})
    response.status_code = http_client.INTERNAL_SERVER_ERROR
    return response


if __name__ == '__main__':
    # This is triggered when running locally (e.g. `python main.py`).
    # Gunicorn is used to run the application on Google App Engine / Kubernetes (Container),
    # and separately configured to handle threading/parallel/async requests -> see entrypoint in Dockerfile.
    app.run(host='localhost', port=8080, threaded=True)
