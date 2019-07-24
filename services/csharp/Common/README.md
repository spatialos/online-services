# `Improbable.MetagameServices.Common`

This package has some extra caveats when building the `.nupkg` file.

The packaging for this project needs to be done with the `nuget` executable, rather than `dotnet`. This is due to a [longstanding issue](https://github.com/nuget/home/issues/3891) where `dotnet pack` does not include referenced projects in the build.

The command to build is:

```bash
nuget pack -Prop Configuration=Release -IncludeReferencedProjects
```

When making changes, you will need to ensure that any updates to dependencies or version number are reflected in _both_ the `.csproj` and `.nuspec` files.

This document will be updated if Microsoft fix the issue.
