#!/usr/bin/env bash

# https://explainshell.com
set -uo pipefail
[[ -n "${DEBUG-}" ]] && set -x
cd "$(dirname "$0")/../"

PROJECT_DIR="$(pwd)"
TEST_RESULTS_DIR="${PROJECT_DIR}"

pushd services/csharp
dotnet restore --no-cache
dotnet build
TEST_FAILED=0
for n in `find . -maxdepth 1 -type d -name '*.Test'`;
do
    if ! dotnet test "./${n}" --logger:"nunit;LogFilePath=${TEST_RESULTS_DIR}/${n}.xml"; then
        TEST_FAILED=1
    fi
done

set -e

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
exit $TEST_FAILED
