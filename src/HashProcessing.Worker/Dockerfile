FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["HashProcessing.Worker/HashProcessing.Worker.csproj", "HashProcessing.Worker/"]
COPY ["HashProcessing.Contracts/HashProcessing.Contracts.csproj", "HashProcessing.Contracts/"]
RUN dotnet restore "HashProcessing.Worker/HashProcessing.Worker.csproj"
COPY . .
WORKDIR /src/HashProcessing.Worker
RUN dotnet publish "./HashProcessing.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS final
WORKDIR /app
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HashProcessing.Worker.dll"]
