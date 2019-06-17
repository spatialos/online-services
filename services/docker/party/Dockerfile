FROM microsoft/dotnet:2.1-sdk AS build
ARG CONFIG=Release
WORKDIR /csharp

# copy csproj and restore as distinct layers
COPY csharp/Party/*.csproj /csharp/Party/
WORKDIR /csharp/Party
RUN dotnet restore

# copy and publish csharp and libraries
WORKDIR /
COPY csharp/ /csharp
WORKDIR /csharp/Party
RUN dotnet publish -c $CONFIG -o out

FROM microsoft/dotnet:2.1-runtime AS runtime
WORKDIR /csharp
COPY --from=build /csharp/Party/out ./
ENTRYPOINT ["dotnet", "Party.dll"]
