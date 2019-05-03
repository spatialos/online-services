#!/usr/bin/env bash

# https://explainshell.com
set -euo pipefail
[[ -n "${DEBUG-}" ]] && set -x
cd "$(dirname "$0")/../"

pushd services/csharp
dotnet restore --no-cache
dotnet build
dotnet test --filter ".Test"

popd
