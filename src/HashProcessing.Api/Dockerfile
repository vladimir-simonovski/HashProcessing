FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["HashProcessing.Api.csproj", "."]
RUN dotnet restore "HashProcessing.Api.csproj"
COPY . .
WORKDIR /src
RUN dotnet publish "./HashProcessing.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
EXPOSE 8080
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HashProcessing.Api.dll"]
