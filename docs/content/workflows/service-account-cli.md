# Platform service-account CLI
<%(TOC)%>

This tool provides a simple CLI built on top of the SpatialOS [Platform
SDK](https://docs.improbable.io/reference/latest/platform-sdk/introduction) for creating, viewing and
deleting service accounts.

You can find out about Platform service accounts in the [SpatialOS Platform SDK documentation](https://docs.improbable.io/reference/latest/platform-sdk/reference/service-accounts#service-accounts).

>**Note:** The Platform service-account CLI is different to the [SpatialOS CLI](https://docs.improbable.io/reference/latest/shared/spatialos-cli-introduction) tool which you use as part of your SpatialOS game development.


You can either run the service-account CLI via editing program arguments and running it via a C# IDE, or you can
use `dotnet` on the command line. For example,  `dotnet run -- <service account CLI tool arguments>`.

## Setup

Run `spatial auth login` before using the tool.

The tool uses your _own_ refresh token to authenticate with SpatialOS, and this will ensure it's up to
date.

## Create a Platform service account

Creates a Platform service account and outputs the corresponding refresh token at the given file.

```
> dotnet run -- create --help
ServiceAccountCLI 1.0.0
Copyright (C) 2019 Improbable Worlds Limited

  --project_name                 Required. The name of your spatial project

  --service_account_name         Required. The name of the Platform service account

  --refresh_token_output_file    Required. The name of a file to output the refresh token to

  --lifetime                     (Default: 1.0:0) The lifetime of the Platform service account as a TimeSpan e.g. days.hours:minutes  - the default corresponds to 1 day.

  --project_write                (Default: false) Whether or not project write access is needed

  --metrics_read                 (Default: false) Whether or not read access to metrics is needed

  --help                         Display this help screen.

  --version                      Display version information.
```

**IMPORTANT**: If no lifetime is specified, the default lifetime of a Platform service account is 1 day.

## List Platform service accounts

Lists all Platform service accounts belonging to project including their names, IDs, permissions and
lifetimes.

```
> dotnet run -- list --help
ServiceAccountCLI 1.0.0
Copyright (C) 2019 Improbable Worlds Limited

  --project_name    Required. The name of your spatial project

  --help            Display this help screen.

  --version         Display version information.
```

Example output:

```
> dotnet run -- list --project_name my_project
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

## Delete a Platform service account

Deletes a Platform service account given its ID.

**IMPORTANT**: Deleting an active Platform service account will cause any services using it to fail to authenticate
with SpatialOS, make sure an account is not in use before deleting it!

```
> dotnet run -- delete --service_account_id my_service_account_id
ServiceAccountCLI 1.0.0
Copyright (C) 2019 Improbable Worlds Limited

  --service_account_id    Required. The ID of the Platform service account

  --help                  Display this help screen.

  --version               Display version information.
```


<%(Nav hide="next")%>
<%(Nav hide="prev")%>

<br/>------------<br/>
_2019-07-16 Page added with limited editorial review_
[//]: # (TODO: https://improbableio.atlassian.net/browse/DOC-1135)
