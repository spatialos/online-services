#!/usr/bin/env bash

# https://explainshell.com
set -euo pipefail
[[ -n "${DEBUG-}" ]] && set -x
cd "$(dirname "$0")/../"

pushd services/csharp
dotnet restore --no-cache
dotnet build
dotnet test --filter ".Test"

pushd IntegrationTest
export SPATIAL_PROJECT="gamex"
export SPATIAL_REFRESH_TOKEN
SPATIAL_REFRESH_TOKEN=$(imp-ci secrets read --environment=production --buildkite-org=improbable --secret-type=spatialos-service-account --secret-name="online-services-test" | jq -Mr '.token')
sh test.sh --test_all
popd

popd
