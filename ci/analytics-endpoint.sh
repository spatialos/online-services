#!/usr/bin/env bash
set -e

finish() {
  # Stops and removes all containers:
  docker-compose -f services/docker/docker_compose_local_analytics_pipeline.yml down
  docker-compose -f services/docker/docker_compose_local_analytics_pipeline.yml rm --force
  # Remove /tmp/ci-online-services:
  rm -rf /tmp/ci-online-services || exit 0
}

# Execute finish() on script exit:
trap finish EXIT

# Set environment variables for docker-compose:
export GOOGLE_PROJECT_ID=logical-flame-194710
export GOOGLE_SECRET_KEY_JSON_ANALYTICS_GCS_WRITER=/tmp/ci-online-services/secrets/analytics-gcs-writer.json
export GOOGLE_SECRET_KEY_P12_ANALYTICS_GCS_WRITER=/tmp/ci-online-services/secrets/analytics-gcs-writer.p12
export GOOGLE_SECRET_KEY_JSON_ANALYTICS_ENDPOINT=/tmp/ci-online-services/secrets/analytics-endpoint.json
export API_KEY=/tmp/ci-online-services/secrets/api-key.json
export IMAGE=analytics-endpoint

# Build container:
docker build -f services/docker/analytics-endpoint/Dockerfile -t gcr.io/${GOOGLE_PROJECT_ID}/${IMAGE}:latest ./services

# Refresh /tmp/ci-online-services:
rm -rf /tmp/ci-online-services || true
mkdir /tmp/ci-online-services

# Grab secrets from Vault:
imp-ci secrets read --environment=production --buildkite-org=improbable --secret-type=gce-key-pair --secret-name=${GOOGLE_PROJECT_ID}/analytics-gcs-writer-json --write-to=${GOOGLE_SECRET_KEY_JSON_ANALYTICS_GCS_WRITER}
imp-ci secrets read --environment=production --buildkite-org=improbable --secret-type=generic-token --secret-name=${GOOGLE_PROJECT_ID}/analytics-gcs-writer-p12 --write-to=/tmp/ci-online-services/secrets/analytics-gcs-writer-p12.json
imp-ci secrets read --environment=production --buildkite-org=improbable --secret-type=gce-key-pair --secret-name=${GOOGLE_PROJECT_ID}/analytics-endpoint-json --write-to=${GOOGLE_SECRET_KEY_JSON_ANALYTICS_ENDPOINT}
imp-ci secrets read --environment=production --buildkite-org=improbable --secret-type=generic-token --secret-name=${GOOGLE_PROJECT_ID}/api-key --write-to=${API_KEY}

# We stored the .p12 key in a JSON object, the main file being retrievable through the key `token`:
cat /tmp/ci-online-services/secrets/analytics-gcs-writer-p12.json | jq -r .token > ${GOOGLE_SECRET_KEY_P12_ANALYTICS_GCS_WRITER}

# Start a local pod containing both containers:
docker-compose -f services/docker/docker_compose_local_analytics_pipeline.yml up --detach && sleep 10

# Parse API key:
API_KEY_TOKEN=$(echo $(cat ${API_KEY}) | jq -r .token)

# Verify v1/event is working:
POST=$(curl -s --request POST --header "content-type:application/json" --data "{\"eventSource\":\"client\",\"eventClass\":\"buildkite\",\"eventType\":\"docker-compose\",\"eventTimestamp\":1562599755,\"eventIndex\":6,\"sessionId\":\"f58179a375290599dde17f7c6d546d78\",\"versionId\":\"2.0.13\",\"eventEnvironment\":\"testing\",\"eventAttributes\":{\"playerId\": 12345678}}" "http://0.0.0.0:8080/v1/event?key=${API_KEY_TOKEN}&analytics_environment=buildkite&event_category=event&session_id=f58179a375290599dde17f7c6d546d78")
echo ${POST}
STATUS_CODE=$(echo ${POST} | jq .code)
echo ${STATUS_CODE}
if [ "${STATUS_CODE}" != "200" ]; then echo 'Error: v1/event did not return 200!' && exit 1; fi;

# Verify v1/file is working:
POST=$(curl -s --request POST --header "content-type:application/json" --data "{\"content_type\":\"text/plain\", \"md5_digest\": \"XKvMhvwrORVuxdX54FQEdg==\"}" "http://0.0.0.0:8080/v1/file?key=${API_KEY_TOKEN}&analytics_environment=buildkite&event_category=file&file_parent=parent&file_child=child")
echo ${POST}
STATUS_CODE=$(echo ${POST} | jq .code)
echo ${STATUS_CODE}
if [ "${STATUS_CODE}" != "200" ]; then echo 'Error: v1/file did not return 200!' && exit 1; fi;
