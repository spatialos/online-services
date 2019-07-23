# Base image:
FROM python:3.7

# Downloading gcloud package:
RUN curl https://dl.google.com/dl/cloudsdk/release/google-cloud-sdk.tar.gz > /tmp/google-cloud-sdk.tar.gz

# Installing the gcloud package:
RUN mkdir -p /usr/local/gcloud \
  && tar -C /usr/local/gcloud -xvf /tmp/google-cloud-sdk.tar.gz \
  && /usr/local/gcloud/google-cloud-sdk/install.sh

# Adding the package path to local:
ENV PATH $PATH:/usr/local/gcloud/google-cloud-sdk/bin

# Install kubectl:
RUN gcloud components install kubectl

# Mount volume into image:
ADD python/analytics-pipeline/src /app/python/analytics-pipeline/src

# Change working directory:
WORKDIR /app

# Upgrade Python's package manger pip:
RUN pip install --upgrade pip

# Install requirements:
RUN pip install -r python/analytics-pipeline/src/requirements/endpoint.txt

# Currently using the gevent asynchronous worker, which is appropriate for I/O processing:
ENTRYPOINT ["gunicorn", "--chdir", "/app/python/analytics-pipeline/src/endpoint/", "main:app", "-b", ":8080", "-w", "2", "-k", "gevent", "--worker-connections", "1000"]

# gunicorn
# in main.py run app
# -b: The socket to bind.
# -w: The number of worker processes for handling requests.
# -k: The type of workers to use (gevent is an async worker).
# --worker-connections: The maximum number of simultaneous clients.
