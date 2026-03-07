FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/HashProcessing.Api/HashProcessing.Api.csproj", "src/HashProcessing.Api/"]
RUN dotnet restore "src/HashProcessing.Api/HashProcessing.Api.csproj"
COPY . .
WORKDIR "/src/src/HashProcessing.Api"
RUN dotnet build "./HashProcessing.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./HashProcessing.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HashProcessing.Api.dll"]
