FROM microsoft/dotnet:2.1-sdk AS build
ARG CONFIG=Release
WORKDIR /csharp

# copy csproj and restore as distinct layers
COPY csharp/DeploymentPool/*.csproj /csharp/DeploymentPool/
WORKDIR /csharp/DeploymentPool
RUN dotnet restore -s https://api.nuget.org/v3/index.json

# copy and publish csharp and libraries
WORKDIR /
COPY csharp/ /csharp
WORKDIR /csharp/DeploymentPool
RUN dotnet publish -c $CONFIG -o out

FROM microsoft/dotnet:2.1-runtime AS runtime
WORKDIR /csharp
COPY --from=build /csharp/DeploymentPool/out ./
ENTRYPOINT ["dotnet", "DeploymentPool.dll"]
