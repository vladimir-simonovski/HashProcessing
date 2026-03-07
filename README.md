# HashProcessing

A distributed SHA-1 hash generation and processing pipeline built with .NET 8, RabbitMQ, and Docker. The API generates hashes, batches them, and publishes to a message queue for parallel consumption by a background worker service.

[Overview](#overview) · [Architecture](#architecture) · [Getting started](#getting-started) · [API](#api) · [Project structure](#project-structure)

## Overview

HashProcessing is a two-service system designed for high-throughput hash generation and processing:

- **API** — ASP.NET Core Minimal API that generates SHA-1 hashes using `System.Security.Cryptography`, streams them through a bounded `Channel<T>`, batches them, and publishes to RabbitMQ.
- **Worker** — Background service that consumes hash batches from RabbitMQ for downstream processing and persistence.

### Features

- Streaming hash generation via `System.Threading.Channels` for backpressure-aware, non-blocking pipelines
- Configurable parallel hash generation (`Parallel.ForAsync`) and parallel batch publishing
- Batched RabbitMQ publishing with persistent delivery mode
- Clean Architecture with DDD tactical patterns (value objects, domain service ports, CQRS command handlers)
- Multi-stage Docker builds for both services
- HTTPS-enabled local development with scripted certificate bootstrap
- OpenAPI/Swagger UI in development mode

## Architecture

The solution follows **Clean Architecture** with inward-only dependency flow:

```
Core (Domain)  ←  Application  ←  Infrastructure
```

| Layer | Responsibility | Key types |
|---|---|---|
| **Core** | Value objects, domain service ports | `Sha1Hash`, `IHash`, `IHashGenerator`, `IHashProcessor` |
| **Application** | Use-case handlers (CQRS) | `GenerateHashesCommand`, `GenerateHashesCommandHandler` |
| **Infrastructure** | Concrete implementations | `DefaultHashGenerator`, `ParallelHashGenerator`, `RabbitMqBatchedOffloadToWorkerProcessor` |

**Processing pipeline:**

```
POST /hashes → GenerateHashesCommandHandler
  → IHashGenerator.StreamSha1s()        // ChannelReader<Sha1Hash>
  → IHashProcessor.ProcessAsync()       // batch + parallel publish
  → RabbitMQ queue "hash-processing"
  → 202 Accepted
```

## Getting started

### Prerequisites

- [.NET SDK 8.x](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or compatible Docker Engine with Compose)

### Run with Docker (recommended)

1. Generate and trust a local HTTPS development certificate:

   ```bash
   chmod +x scripts/setup-dev-https.sh
   ./scripts/setup-dev-https.sh
   ```

   The script generates an ASP.NET Core dev certificate, exports it as a PFX for Docker, and trusts it on your machine (may prompt for your password on macOS).

2. Start both services:

   ```bash
   docker compose up --build
   ```

3. Open the API:

   | Endpoint | URL |
   |---|---|
   | HTTP | `http://localhost:8080` |
   | HTTPS | `https://localhost:8081` |
   | Swagger UI | `https://localhost:8081/swagger` |

> [!NOTE]
> Override the certificate password by setting `HTTPS_CERT_PASSWORD` before running the setup script.
> If your browser still shows a certificate warning after running the script, try a private/incognito window or clear the browser's cached certificates.

### Run without Docker

```bash
dotnet run --project src/HashProcessing.Api
```

The API will be available at `http://localhost:5031` (and `https://localhost:7093`).

> [!IMPORTANT]
> A running RabbitMQ instance on `localhost` is required for hash processing to work outside Docker.

## API

| Method | Route | Description |
|---|---|---|
| `POST` | `/hashes` | Generate 40,000 SHA-1 hashes, batch and publish to RabbitMQ. Returns `202 Accepted`. |
| `GET` | `/hashes` | Retrieve hash counts grouped by date (placeholder — DB integration pending). |

### Example

```bash
# Trigger hash generation
curl -X POST https://localhost:8081/hashes
```

## Running tests

```bash
dotnet test
```

Tests use **xUnit** with **NSubstitute** for mocking. Current coverage includes the `RabbitMqBatchedOffloadToWorkerProcessor` batch-and-publish pipeline.

## Project structure

```text
HashProcessing/
├── compose.yaml                         # Docker Compose (API + Worker)
├── global.json                          # .NET SDK version pinning
├── scripts/
│   └── setup-dev-https.sh               # HTTPS certificate generation + trust
├── src/
│   ├── HashProcessing.Api/
│   │   ├── Program.cs                   # Host builder, endpoints, middleware
│   │   ├── Dockerfile                   # Multi-stage build → Alpine ASP.NET 8.0
│   │   ├── Application/                 # CQRS command + handler, DI
│   │   ├── Core/                        # Domain: Sha1Hash, interfaces
│   │   └── Infrastructure/              # RabbitMQ processor, hash generators
│   └── HashProcessing.Worker/
│       ├── Program.cs                   # Host builder
│       ├── Worker.cs                    # BackgroundService consumer
│       └── Dockerfile                   # Multi-stage build → Alpine .NET Runtime 8.0
└── tests/
    └── HashProcessing.Api.UnitTests/    # xUnit + NSubstitute
```

## Tech stack

| Component | Technology |
|---|---|
| Framework | .NET 8 / ASP.NET Core Minimal API |
| Messaging | RabbitMQ (`RabbitMQ.Client` 7.x) |
| Concurrency | `System.Threading.Channels`, `Parallel.ForAsync` |
| API docs | OpenAPI / Swashbuckle |
| Containers | Docker multi-stage Alpine builds, Docker Compose |
| Testing | xUnit, NSubstitute, coverlet |
