FROM microsoft/dotnet:2.1-sdk AS build
ARG CONFIG=Release
WORKDIR /csharp

# copy csproj and restore as distinct layers
COPY csharp/PlayFabAuth/*.csproj /csharp/PlayFabAuth/
WORKDIR /csharp/PlayFabAuth
RUN dotnet restore -s https://api.nuget.org/v3/index.json

# copy and publish csharp and libraries
WORKDIR /
COPY csharp/ /csharp
WORKDIR /csharp/PlayFabAuth
RUN dotnet publish -c $CONFIG -o out

FROM microsoft/dotnet:2.1-runtime AS runtime
WORKDIR /csharp
COPY --from=build /csharp/PlayFabAuth/out ./
ENTRYPOINT "dotnet" "PlayFabAuth.dll" --grpc_service_port "$SERVICE_PORT" --playfab_secret_key "$PLAYFAB_SECRET_KEY" --playfab_title_id "$PLAYFAB_TITLE_ID" --spatial_refresh_token "$SPATIAL_REFRESH_TOKEN" --spatial_project "$SPATIAL_PROJECT"
