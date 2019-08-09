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

# Mount volumes into image:
ADD python/analytics-pipeline/src /app/python/analytics-pipeline/src
ADD docker/analytics-endpoint/ /app/bash/

# Change working directory:
WORKDIR /app

# Upgrade Python's package manger pip:
RUN pip install --upgrade pip

# Install requirements:
RUN pip install -r python/analytics-pipeline/src/requirements/endpoint.txt

# Provide default entrypoint:
ENTRYPOINT bash/entrypoint.sh
