# Python 3.7.1

import Crypto.PublicKey.RSA as RSA
import subprocess
import logging
import hashlib
import string
import json
import gzip
import os

from common.functions import get_date_time, try_format_improbable_event, try_format_playfab_event, \
    try_format_unknown_event
from common.classes import CloudStorageURLSigner
from flask import Flask, jsonify, request
from six.moves import http_client
from google.cloud import storage
from random import choices

# Provision GCS Client & Bucket for `v1/event`:
client_storage = storage.Client.from_service_account_json(os.environ['GOOGLE_SECRET_KEY_JSON_ANALYTICS_GCS_WRITER'])
bucket = client_storage.get_bucket(os.environ['ANALYTICS_BUCKET_NAME'])

# Provision URL Signer for `v1/file`:
with open(os.environ['GOOGLE_SECRET_KEY_DER_ANALYTICS_GCS_WRITER'], 'rb') as f:
    key_der = f.read()
private_key = RSA.importKey(key_der)
signer = CloudStorageURLSigner(private_key, os.environ['GOOGLE_SERVICE_ACCOUNT_EMAIL_ANALYTICS_GCS_WRITER'])

app = Flask(__name__)


@app.route('/v1/event', methods=['POST'])
def store_event_in_gcs(bucket=bucket, bucket_name=os.environ['ANALYTICS_BUCKET_NAME']):
    try:
        ts_fmt, event_ds, event_time = get_date_time()
        random = ''.join(choices(string.ascii_uppercase + string.digits, k=6))

        event_schema = request.args.get('event_schema', 'unknown') or 'unknown'  # (parameter, default_value) or parameter_value_if_none
        event_environment = request.args.get('event_environment', 'unknown') or 'unknown'
        event_category = request.args.get('event_category', 'external') or 'external'
        event_ds = request.args.get('event_ds', event_ds) or event_ds
        event_time = request.args.get('event_time', event_time) or event_time
        session_id = request.args.get('session_id', 'session_id_not_available') or 'session_id_not_available'

        try:
            payload = request.get_json(force=True)

            object_location, object_location_raw = [
                f'data_type=jsonl/event_schema={_event_schema}/event_category={event_category}/event_environment={event_environment}/event_ds={event_ds}/event_time={event_time}/{session_id}/{ts_fmt}-{random}'
                for _event_schema in [event_schema, f'{event_schema}-raw']]
            gspath_json = f'gs://{bucket_name}/{object_location}.jsonl'
            batch_id_json = hashlib.md5(gspath_json.encode('utf-8')).hexdigest()
            events_formatted, events_raw = [], []

            # If dict nest in list:
            if isinstance(payload, dict):
                payload = [payload]

            # Parse list:
            if isinstance(payload, list):
                for index, event in enumerate(payload):

                    if event_schema == 'improbable':
                        success, tried_event = try_format_improbable_event(index, event, batch_id_json, os.environ['ANALYTICS_ENVIRONMENT'])
                    elif event_schema == 'playfab':
                        success, tried_event = try_format_playfab_event(index, event, batch_id_json, os.environ['ANALYTICS_ENVIRONMENT'])
                    else:
                        success, tried_event = try_format_unknown_event(index, event, batch_id_json, os.environ['ANALYTICS_ENVIRONMENT'])

                    if success:
                        events_formatted.append(json.dumps(tried_event))
                    else:
                        events_raw.append(json.dumps(tried_event))

            # Write formatted JSON events:
            if len(events_formatted) > 0:
                blob = bucket.blob(f'{object_location}.jsonl')
                blob.content_encoding = 'gzip'
                blob.upload_from_string(gzip.compress(bytes('\n'.join(events_formatted), encoding='utf-8')), content_type='text/plain; charset=utf-8')

            # Write raw JSON events:
            if len(events_raw) > 0:
                blob = bucket.blob(f'{object_location_raw}.jsonl')
                blob.content_encoding = 'gzip'
                blob.upload_from_string(gzip.compress(bytes('\n'.join(events_raw), encoding='utf-8')), content_type='text/plain; charset=utf-8')

            return jsonify({'statusCode': 200})

        except Exception:
            payload = request.get_data(as_text=True)
            object_location_unknown = f'data_type=unknown/event_schema={event_schema}/event_category={event_category}/event_environment={event_environment}/event_ds={event_ds}/event_time={event_time}/{session_id}/{ts_fmt}-{random}'
            blob = bucket.blob(object_location_unknown)
            blob.upload_from_string(payload, content_type='text/plain; charset=utf-8')

            return jsonify({'statusCode': 200})

    except Exception as e:
        return jsonify({'message': f'Exception: {type(e).__name__}', 'args': e.args})


@app.route('/v1/file', methods=['POST'])
def return_signed_url_gcs():
    try:
        payload = request.get_json(force=True)
        ts_fmt, file_ds, file_time = get_date_time()

        file_category = request.args.get('file_category', 'unknown') or 'unknown'  # (parameter, default_value) or parameter_value_if_none
        file_ds = request.args.get('file_ds', file_ds) or file_ds
        file_time = request.args.get('file_time', file_time) or file_time
        file_parent = request.args.get('file_parent', 'unknown') or 'unknown'
        file_child = request.args.get('file_child', 'unknown') or 'unknown'

        random = ''.join(choices(string.ascii_uppercase + string.digits, k=6))
        object_location = f'data_type=file/file_category={file_category}/file_ds={file_ds}/file_time={file_time}/{file_parent}/{file_child}-{random}'
        bucket_name = os.environ['ANALYTICS_BUCKET_NAME']
        file_path = f'/{bucket_name}/{object_location}'
        signed = signer.put(path=file_path, content_type=payload['content_type'], md5_digest=payload['md5_digest'])
        return jsonify(signed)

    except Exception as e:
        return jsonify({'message': f'Exception: {type(e).__name__}', 'args': e.args})


@app.errorhandler(http_client.INTERNAL_SERVER_ERROR)
def unexpected_error(e):
    """Handle exceptions by returning swagger-compliant json."""
    logging.exception('An error occured while processing the request.')
    response = jsonify({
        'code': http_client.INTERNAL_SERVER_ERROR,
        'message': f'Exception: {e}'})
    response.status_code = http_client.INTERNAL_SERVER_ERROR
    return response


if __name__ == '__main__':
    # This is triggered when running locally (e.g. `python main.py`).
    # Gunicorn is used to run the application on Google App Engine / Kubernetes (Container),
    # and separately configured to handle threading/parallel/async requests -> see entrypoint in Dockerfile.
    app.run(host='localhost', port=8080, threaded=True)
