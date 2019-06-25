# Python 3.6.5

import Crypto.PublicKey.RSA as RSA
import subprocess
import logging
import hashlib
import json
import os

from common.functions import formatEvent, dateTime
from common.classes import CloudStorageURLSigner
from flask import Flask, jsonify, request
from six.moves import http_client
from google.cloud import storage
from random import randint

client_storage = storage.Client.from_service_account_json(os.environ['SECRET_JSON'])
bucket = client_storage.get_bucket(os.environ['BUCKET_NAME'])

try:
    try:
        subprocess.check_call('base64 --decode %s > /tmp/analytics-gcs-writer.p12' % os.environ['SECRET_P12'], shell = True)
    except:
        subprocess.call('cp %s /tmp/analytics-gcs-writer.p12' % os.environ['SECRET_P12'], shell = True)
    subprocess.call('openssl pkcs12 -passin pass:notasecret -in /tmp/analytics-gcs-writer.p12 -nodes -nocerts > /tmp/analytics-gcs-writer.pem', shell = True)
    subprocess.call('openssl rsa -in /tmp/analytics-gcs-writer.pem -inform PEM -out /tmp/analytics-gcs-writer.der -outform DER', shell = True)
    key_der = open('/tmp/analytics-gcs-writer.der', 'rb').read()
    private_key = RSA.importKey(key_der)
    signer = CloudStorageURLSigner(private_key, os.environ['EMAIL'])
except:
    print("Could not convert .p12 key into DER format! File endpoint not available..")

app = Flask(__name__)

@app.route('/v1/event', methods=['POST'])
def storeEventGcs(bucket = bucket, bucket_name = os.environ['BUCKET_NAME']):
    try:
        ts, ts_fmt, ds, event_time = dateTime()

        analytics_environment = request.args.get('analytics_environment', 'development') # (parameter, default_value)
        event_category = request.args.get('event_category', 'cold')
        event_ds = request.args.get('ds', ds)
        event_time = request.args.get('time', event_time)
        session_id = request.args.get('session_id', 'session-id-not-available')

        gcs_uri = 'data_type={data_type}/analytics_environment={analytics_environment}/event_category={event_category}/event_ds={event_ds}/event_time={event_time}/{session_id}/{ts_fmt}-{int}'

        try:
            payload = request.get_json(force = True)
            gcs_destination = gcs_uri.format(data_type = 'json', analytics_environment = analytics_environment, event_category = event_category, event_ds = event_ds, event_time = event_time, session_id = session_id, ts_fmt = ts_fmt, int = randint(100000, 999999))

            batch_id = hashlib.md5('/'.join(gcs_destination.split('/')[-2:]).encode('utf-8')).hexdigest()

            if isinstance(payload, list):
                # if addTimestamp fails it means element is not a dict, operation fails & we except into the other parse flow
                events = [json.dumps(formatEvent(index, event, batch_id, analytics_environment)) for index, event in enumerate(payload)]
            elif isinstance(payload, dict):
                events = [json.dumps(formatEvent(0, payload, batch_id, analytics_environment))]

            blob = bucket.blob(gcs_destination)
            blob.upload_from_string('\n'.join(events), content_type = 'text/plain; charset=utf-8')

            return jsonify({'code': 200, 'message': 'gs://{bucket_name}/{gcs_uri}'.format(bucket_name = bucket_name, gcs_uri = gcs_destination)})

        except:
            payload = request.get_data(as_text = True)
            gcs_destination = gcs_uri.format(data_type = 'unknown', analytics_environment = analytics_environment, event_category = event_category, event_ds = event_ds, event_time = event_time, session_id = session_id, ts_fmt = ts_fmt, int = randint(100000, 999999))
            blob = bucket.blob(gcs_destination)
            blob.upload_from_string(payload, content_type = 'text/plain; charset=utf-8')

            return jsonify({'code': 200, 'message': 'gs://{bucket_name}/{gcs_uri}'.format(bucket_name = bucket_name, gcs_uri = gcs_destination)})

    except Exception as e:
        return jsonify({'message': 'Exception: {e}'.format(e = type(e).__name__), 'args': e.args})

@app.route('/v1/file', methods=['POST'])
def returnSignedUrlGcs():
    try:
        ts, ts_fmt, ds, event_time = dateTime()

        analytics_environment = request.args.get('analytics_environment', 'development') # (parameter, default_value)
        event_category = request.args.get('event_category', 'crashdump-worker')
        event_ds = request.args.get('ds', ds)
        event_time = request.args.get('time', event_time)
        file_parent = request.args.get('file_parent', 'unknown')
        file_child = request.args.get('file_child', 'unknown')

        payload = request.get_json(force = True)

        gcs_uri = 'data_type={data_type}/analytics_environment={analytics_environment}/event_category={event_category}/event_ds={event_ds}/event_time={event_time}/{file_parent}/{file_child}-{int}'
        gcs_destination = gcs_uri.format(data_type = 'file', analytics_environment = analytics_environment, event_category = event_category, event_ds = event_ds, event_time = event_time, file_parent = file_parent, file_child = file_child, int = randint(100000, 999999))

        file_path = '/{bucket_name}/{object_name}'.format(bucket_name = os.environ['BUCKET_NAME'], object_name = gcs_destination)
        signed = signer.Put(path = file_path, content_type = payload['content_type'], md5_digest = payload['md5_digest'])
        return jsonify(signed)

    except Exception as e:
        return jsonify({'message': 'Exception: {e}'.format(e = type(e).__name__), 'args': e.args})

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
    app.run(host = 'localhost', port = 8080, debug = True, threaded = True)
