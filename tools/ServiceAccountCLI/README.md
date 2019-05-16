# Service Account CLI Tool

This tool provides a simple CLI built on top of the [Platform
SDK](https://docs.improbable.io/reference/latest/platform-sdk/introduction) for creating, viewing and
deleting service accounts.

You can read up on service accounts
[here](https://docs.improbable.io/reference/latest/platform-sdk/reference/service-accounts#service-accounts).

## Usage

You can either run the tool via editing program arguments and running it via a C# IDE, or you can
use `dotnet` on the command line i.e. `dotnet run --project ServiceAccountCLI -- <service account CLI tool arguments>`

### Setup

Run `spatial auth login` before using the tool.

The tool uses your _own_ refresh token to authenticate with SpatialOS, and this will ensure it's up to
date.

### Creating a Service Account

Creates a service account and outputs the corresponding refresh token at the given file.

```
> dotnet run --project ServiceAccountCLI -- create --help
ServiceAccountCLI 1.0.0
Copyright (C) 2019 Improbable Worlds Limited

  --project_name                 Required. The name of your spatial project

  --service_account_name         Required. The name of the service account

  --refresh_token_output_file    Required. The name of a file to output the refresh token to

  --lifetime                     (Default: 1.0:0) The lifetime of the service account as a TimeSpan (e.g. days.hours:minutes)

  --project_write                (Default: false) Whether or not project write access is needed

  --metrics_read                 (Default: false) Whether or not read access to metrics is needed

  --help                         Display this help screen.

  --version                      Display version information.
```

IMPORTANT: If no lifetime is specified, the default lifetime of a service account is 1 day.

### Listing Service Accounts

Lists all service accounts belonging to project including their names, IDs, permissions and
lifetimes.

```
> dotnet run --project ServiceAccountCLI -- list --help
ServiceAccountCLI 1.0.0
Copyright (C) 2019 Improbable Worlds Limited

  --project_name    Required. The name of your spatial project

  --help            Display this help screen.

  --version         Display version information.
```

Example output

```
> dotnet run --project ServiceAccountCLI -- list --project_name my_project
-----------------------------
Name: matchmaking_service_account
ID: 4642391881940992
Creation time : "2019-04-22T13:24:48.903247Z"
Expiring in 23 day(s)
Permissions: [ { "verbs": [ "READ", "WRITE" ], "parts": [ "prj", "my_project", "*" ] }, { "verbs": [ "READ" ], "parts": [ "srv", "*" ] } ]
-----------------------------
-----------------------------
Name: inventory_service_account
ID: 4645287763640320
Creation time : "2019-04-24T14:04:40.599941Z"
Expiring in 24 day(s)
Permissions: [ { "verbs": [ "READ", "WRITE" ], "parts": [ "prj", "my_project", "*" ] }, { "verbs": [ "READ" ], "parts": [ "srv", "*" ] } ]
-----------------------------
```

### Deleting a Service Account

Deletes a service account given its ID.

IMPORTANT: Deleting an active service account will cause services to fail, make sure the account is
not in use before deleting!

```
> dotnet run --project ServiceAccountCLI -- delete --service_account_id my_service_account_id
ServiceAccountCLI 1.0.0
Copyright (C) 2019 Improbable Worlds Limited

  --service_account_id    Required. The ID of the service account

  --help                  Display this help screen.

  --version               Display version information.
```
