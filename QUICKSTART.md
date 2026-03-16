# Quick Start

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (or compatible Docker Engine with Compose)
- [PowerShell 7.4+](https://learn.microsoft.com/en-us/powershell/scripting/install/installing-powershell)

## Start the stack

```powershell
# Generate and trust a local HTTPS dev certificate (one-time)
./scripts/setup-dev-https.ps1

# Start API, Worker, RabbitMQ, and MariaDB
docker compose up --build
```

Wait until you see both `hashprocessing-api` and `hashprocessing-worker` reporting healthy logs.

## Try it

```bash
# Generate 40,000 SHA-1 hashes (default)
curl -X POST https://localhost:8081/hashes

# Generate a custom count
curl -X POST 'https://localhost:8081/hashes?count=10000'

# Retrieve aggregated daily hash counts
curl https://localhost:8081/hashes
```

### Expected output

`POST /hashes` returns immediately with `202 Accepted`. The Worker processes batches in the background.

After a few seconds, `GET /hashes` returns aggregated daily counts:

```json
{
  "hashes": [
    {
      "date": "2026-03-15",
      "count": 40000
    }
  ]
}
```

## Useful URLs

| Service | URL |
|---|---|
| API (HTTP) | `http://localhost:8080` |
| API (HTTPS) | `https://localhost:8081` |
| Swagger UI | `https://localhost:8081/swagger` |
| RabbitMQ Management | `http://localhost:15672` (guest / guest) |

## Stop

```bash
docker compose down
```
