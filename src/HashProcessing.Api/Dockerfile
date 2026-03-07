FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["HashProcessing.Api/HashProcessing.Api.csproj", "HashProcessing.Api/"]
COPY ["HashProcessing.Contracts/HashProcessing.Contracts.csproj", "HashProcessing.Contracts/"]
RUN dotnet restore "HashProcessing.Api/HashProcessing.Api.csproj"
COPY . .
WORKDIR /src/HashProcessing.Api
RUN dotnet publish "./HashProcessing.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
EXPOSE 8080
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HashProcessing.Api.dll"]
