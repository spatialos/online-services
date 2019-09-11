# IntegrationTest project

This project contains a full end-to-end test for two systems.

1. **Deployment Gateway**, which is comprised of the following services:
   * Gateway
   * GatewayInternal
   * Matchers

2. **Party System**, which is comprised of a single service: Party.

## Prerequisites

This test has some additional requirements over the standard unit tests:

- [Docker](https://docs.docker.com/install/)
- [Docker Compose](https://docs.docker.com/compose/)

## Running the tests

To run the test suite, execute the script `test.sh`. You will need a sh-compatible shell. You will need the following environment variables:
- `SPATIAL_REFRESH_TOKEN` - your refresh token, used to create real PITs.
- `SPATIAL_PROJECT` - the name of a SpatialOS project your refresh token has permissions for.
- `PLAYFAB_SECRET_KEY` - your PlayFab secret key, used to authenticate PlayFab users.
- `DEPLOYMENT_METADATA_SERVER_SECRET` - your deployment metadata secret.

### Command line arguments:
- `--no_rebuild`: skips the Docker build, resulting in faster iteration if modifying the test.
- `--test_all`: runs all existing integration tests.
- `--test_party`: runs all integration tests related to the Party System.
- `--test_invite`: runs all integration tests related to the Invite System.
- `--test_matchmaking`: runs all integration tests related to the Matchmaking System.
- `--test_playfab_auth`: runs all integration tests related to the PlayFab Auth System.
