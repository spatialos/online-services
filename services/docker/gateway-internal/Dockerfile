FROM microsoft/dotnet:2.1-sdk AS build
ARG CONFIG=Release
WORKDIR /csharp

# copy csproj and restore as distinct layers
COPY csharp/GatewayInternal/*.csproj /csharp/GatewayInternal/
WORKDIR /csharp/GatewayInternal
RUN dotnet restore

# copy and publish csharp and libraries
WORKDIR /
COPY csharp/ /csharp
WORKDIR /csharp/GatewayInternal
RUN dotnet publish -c $CONFIG -o out

FROM microsoft/dotnet:2.1-runtime AS runtime
WORKDIR /csharp
COPY --from=build /csharp/GatewayInternal/out ./
ENTRYPOINT ["dotnet", "GatewayInternal.dll"]
