FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/HashProcessing.Worker/HashProcessing.Worker.csproj", "src/HashProcessing.Worker/"]
RUN dotnet restore "src/HashProcessing.Worker/HashProcessing.Worker.csproj"
COPY . .
WORKDIR "/src/src/HashProcessing.Worker"
RUN dotnet build "./HashProcessing.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./HashProcessing.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HashProcessing.Worker.dll"]
