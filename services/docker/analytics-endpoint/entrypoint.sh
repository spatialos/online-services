#!/usr/bin/env bash

# == .p12 Key Conversion ==
# This section largely follows: https://github.com/GoogleCloudPlatform/storage-signedurls-python/blob/master/conf.example.py

# Kubernetes mounts base64 encoded .p12 secrets, which we need to decode first.
# If we are running locally, we will however mount a normal .p12 secret, which we only have to copy to another location.
base64 --decode $GOOGLE_SECRET_KEY_P12_ANALYTICS_GCS_WRITER > /tmp/analytics-gcs-writer.p12 || cp $GOOGLE_SECRET_KEY_P12_ANALYTICS_GCS_WRITER /tmp/analytics-gcs-writer.p12

# Convert p12 into pem:
openssl pkcs12 -passin pass:notasecret -in /tmp/analytics-gcs-writer.p12 -nodes -nocerts > /tmp/analytics-gcs-writer.pem

# Convert pem into DER:
openssl rsa -in /tmp/analytics-gcs-writer.pem -inform PEM -out /tmp/analytics-gcs-writer.der -outform DER

# Set environment variable with location:
export GOOGLE_SECRET_KEY_DER_ANALYTICS_GCS_WRITER=/tmp/analytics-gcs-writer.der

# == Start Endpoint Execution ==

# Start endpoint, using the gevent asynchronous worker, which is appropriate for I/O processing:
gunicorn --chdir /app/python/analytics-pipeline/src/endpoint/ main:app -b :8080 -w 2 -k gevent --worker-connections 1000

# gunicorn
# in main.py run app
# -b: The socket to bind.
# -w: The number of worker processes for handling requests.
# -k: The type of workers to use (gevent is an async worker).
# --worker-connections: The maximum number of simultaneous clients.
