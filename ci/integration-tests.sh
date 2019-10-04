#!/usr/bin/env bash

# https://explainshell.com
set -ueo pipefail
[[ -n "${DEBUG-}" ]] && set -x
cd "$(dirname "$0")/../"

PROJECT_DIR="$(pwd)"
TEST_RESULTS_DIR="${PROJECT_DIR}"

pushd services/csharp

pushd IntegrationTest
export SPATIAL_PROJECT="gamex"
export SPATIAL_REFRESH_TOKEN
SPATIAL_REFRESH_TOKEN=$(imp-ci secrets read --environment=production --buildkite-org=improbable --secret-type=spatialos-service-account --secret-name="online-services-test" | jq -Mr '.token')
export PLAYFAB_SECRET_KEY
PLAYFAB_SECRET_KEY=$(imp-ci secrets read --environment=production --buildkite-org=improbable --secret-type=playfab-secret-key --secret-name="online-services-playfab-secret-key-integ" | jq -Mr '.token')
export TEST_RESULTS_DIR
export COMPOSE_NETWORK_SUFFIX="${BUILDKITE_JOB_ID}"
sh test.sh --test_all
popd

popd
exit 0
