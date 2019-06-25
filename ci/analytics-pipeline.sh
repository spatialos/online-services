#!/usr/bin/env bash
set -e

# Set environment variables for docker-compose:
export GCP=logical-flame-194710
export SECRET_JSON=/tmp/ci-online-services/secrets/analytics-gcs-writer.json
export SECRET_P12=/tmp/ci-online-services/secrets/analytics-gcs-writer.p12
export SECRET_JSON_ESP=/tmp/ci-online-services/secrets/analytics-endpoint.json
export IMAGE=analytics-endpoint-bk
export API_KEY=/tmp/ci-online-services/secrets/api-key.json

# Build container:
docker build -f services/docker/analytics-endpoint/Dockerfile -t gcr.io/${GCP}/${IMAGE}:latest ./services

# Refresh /tmp/ci-online-services:
rm -rf /tmp/ci-online-services || true
mkdir /tmp/ci-online-services

# Grab secrets from Vault:
imp-ci secrets read --environment=production --buildkite-org=improbable --secret-type=gce-key-pair --secret-name=${GCP}/analytics-gcs-writer-json --write-to=/tmp/ci-online-services/secrets/analytics-gcs-writer.json
imp-ci secrets read --environment=production --buildkite-org=improbable --secret-type=generic-token --secret-name=${GCP}/analytics-gcs-writer-p12 --write-to=/tmp/ci-online-services/secrets/analytics-gcs-writer-p12.json
imp-ci secrets read --environment=production --buildkite-org=improbable --secret-type=gce-key-pair --secret-name=${GCP}/analytics-endpoint-json --write-to=/tmp/ci-online-services/secrets/analytics-endpoint.json
imp-ci secrets read --environment=production --buildkite-org=improbable --secret-type=generic-token --secret-name=${GCP}/api-key --write-to=/tmp/ci-online-services/secrets/api-key.json

cat /tmp/ci-online-services/secrets/analytics-gcs-writer-p12.json | jq -r .token > /tmp/ci-online-services/secrets/analytics-gcs-writer.p12

# Start a local pod containing both containers:
docker-compose -f services/docker/docker_compose_local_analytics.yml up --detach

# Parse API key:
API_KEY_TOKEN=$(echo $(cat ${API_KEY}) | jq -r .token)

# Verify v1/event is working:
POST=$(curl -s --request POST --header "content-type:application/json" --data "{\"eventSource\":\"client\",\"eventClass\":\"buildkite\",\"eventType\":\"docker-compose\",\"eventTimestamp\":1562599755,\"eventIndex\":6,\"sessionId\":\"f58179a375290599dde17f7c6d546d78\",\"buildVersion\":\"2.0.13\",\"eventEnvironment\":\"testing\",\"eventAttributes\":{\"playerId\": 12345678}}" "http://0.0.0.0:8080/v1/event?key=${API_KEY_TOKEN}&analytics_environment=buildkite&event_category=event&session_id=f58179a375290599dde17f7c6d546d78")
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

finish() {
  # Stops and removes all containers:
  docker-compose -f services/docker/docker_compose_local_analytics.yml down
  docker-compose -f services/docker/docker_compose_local_analytics.yml rm --force
  # Remove /tmp/ci-online-services:
  rm -rf /tmp/ci-online-services || exit 0
}
trap finish EXIT
