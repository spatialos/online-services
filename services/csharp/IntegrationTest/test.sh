#!/bin/sh

if [ -z "${SPATIAL_REFRESH_TOKEN}" ]; then
  echo "The variable SPATIAL_REFRESH_TOKEN is required."
  exit 1
fi

if [ -z "${SPATIAL_PROJECT}" ]; then
  echo "The variable SPATIAL_PROJECT is required."
  exit 1
fi

if [ -z "${PLAYFAB_SECRET_KEY}" ]; then
  echo "The variable PLAYFAB_SECRET_KEY is required."
  exit 1
fi

args=$*

# Requires an argument. We will check whether the received argument is within the command line arguments of the script.
was_argument_passed() {
    for i in ${args} ; do
        if [ "${i}" = "$1" ]; then
            return 0
        fi
    done
    return 1
}

was_argument_passed "--no_rebuild"
no_rebuild=$?

was_argument_passed "--test_party"
test_party=$?

was_argument_passed "--test_invite"
test_invite=$?

was_argument_passed "--test_matchmaking"
test_matchmaking=$?

was_argument_passed "--test_playfab_auth"
test_playfab_auth=$?

was_argument_passed "--test_all"
test_all=$?

if [ ${test_all} -eq 0 ]; then
    test_party=0
    test_matchmaking=0
    test_invite=0
    test_playfab_auth=0
fi

set -ex

# Receives an argument containing the images that are needed to be built separated by whitespace.
build_images() {
  for image in $1; do
    echo "Building Docker image for ${image}."
    # Builds the docker image in a subshell and returns to the current directory after finishing.
    (cd ../.. && docker build -f "docker/${image}/Dockerfile" -t "improbable-metagameservices-${image}:test" --build-arg CONFIG="Debug" .)
  done
}

# If the `--no_rebuild` command line arg is provided, we should skip building the images.
if [ ${no_rebuild} -eq 1 ]; then
  images_to_build="gateway gateway-internal test-matcher party playfab-auth"
  build_images "${images_to_build}"
fi

finish() {
  # Stops and removes all containers.
  docker-compose -f docker_compose.yml down
  docker-compose -f docker_compose.yml rm --force
}
trap finish EXIT

# Create containers, and then start them backgrounded.
docker-compose -f docker_compose.yml up --no-start
docker-compose -f docker_compose.yml start

# Execute the integration tests depending on the received command line arguments.
if [ ${test_matchmaking} -eq 0 ]; then
  echo "Running tests for the Matchmaking system."
  dotnet test --filter "MatchmakingSystemShould"
fi

if [ ${test_party} -eq 0 ]; then
  echo "Running tests for the Party system."
  dotnet test --filter "PartySystemShould"
fi

if [ ${test_invite} -eq 0 ]; then
  echo "Running tests for the Invite system."
  dotnet test --filter "InviteSystemShould"
fi

if [ ${test_playfab_auth} -eq 0 ]; then
  echo "Running tests for PlayFab Auth system."
  dotnet test --filter "PlayFabAuthShould"
fi
