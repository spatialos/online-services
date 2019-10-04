#!/usr/bin/env bash

# https://explainshell.com
set -uo pipefail
[[ -n "${DEBUG-}" ]] && set -x
cd "$(dirname "$0")/../"

PROJECT_DIR="$(pwd)"
TEST_RESULTS_DIR="${PROJECT_DIR}"

pushd services/csharp
TEST_FAILED=0
for n in `find . -maxdepth 1 -type d -name '*.Test'`;
do
    if ! dotnet test "./${n}" --logger:"nunit;LogFilePath=${TEST_RESULTS_DIR}/${n}.xml"; then
        TEST_FAILED=1
    fi
done
exit $TEST_FAILED