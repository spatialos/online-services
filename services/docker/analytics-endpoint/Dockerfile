# Base image:
FROM python:3.7

# Mount volumes into image:
ADD python/analytics-pipeline/src /app/python/analytics-pipeline/src
ADD docker/analytics-endpoint/ /app/bash/

# Change working directory:
WORKDIR /app

# Upgrade Python's package manager pip:
RUN pip install --upgrade pip

# Install requirements:
RUN pip install -r python/analytics-pipeline/src/requirements/endpoint.txt

# Provide default entrypoint:
ENTRYPOINT bash/entrypoint.sh
