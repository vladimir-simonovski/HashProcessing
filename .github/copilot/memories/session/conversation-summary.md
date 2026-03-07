# Conversation Summary — March 7, 2026

## What was done

### 1. RabbitMQ added to Docker Compose
- Added `rabbitmq:4-management` service with healthcheck, management UI (port 15672), and AMQP (port 5672).
- Created `rabbitmq/definitions.json` to declaratively define topology: `hash-processing` queue (durable, non-auto-delete), guest user, default vhost, and permissions.
- Created `rabbitmq/rabbitmq.conf` with `management.load_definitions` to load definitions on startup.
- Fixed boot failure: `password_hash` field requires base64-encoded hash — switched to `password` field for plaintext.
- Fixed second boot failure: vhosts section was required for queue referencing vhost `/`.

### 2. API → RabbitMQ connectivity fix
- `ServiceCollectionExtensions.cs` updated to read `RabbitMQ:HostName` from `IConfiguration` (fallback to `localhost`).
- `Program.cs` updated to pass `builder.Configuration` to `AddInfrastructure()`.
- Compose passes `RabbitMQ__HostName=rabbitmq` env var to API and Worker.
- Stale Docker image was the root cause of the 127.0.0.1 connection issue — needed `--build`.

### 3. Worker implementation (RabbitMQ consumer + MariaDB persistence)

**Core layer:**
- `HashEntity` — domain entity (`Id`, `Date` as DateOnly, `Sha1`) with constructor validation.
- `IHashRepository` — repository port for batch persistence.

**Infrastructure layer:**
- `RabbitMqHashConsumer` — connects to queue, deserializes `HashBatchMessage`, maps via anti-corruption layer, persists via repository. Manual ack/nack, prefetch=1.
- `HashBatchMessageMapper` — maps contract `Hash` records → domain `HashEntity` (anti-corruption layer).
- `HashDbContext` — EF Core context mapping `HashEntity` to `hashes` table with index on `date`.
- `HashRepository` — scoped EF Core persistence using `IServiceScopeFactory`.
- `HashDbContextDesignTimeFactory` — `IDesignTimeDbContextFactory` for `dotnet ef` CLI without a live DB.
- `ServiceCollectionExtensions` — DI wiring for all infrastructure services.

**Worker:**
- `Worker.cs` rewritten as `BackgroundService` launching 4 parallel `RabbitMqHashConsumer` tasks.
- `Program.cs` — registers infrastructure, runs `db.Database.MigrateAsync()` on startup.

**Docker/Compose:**
- Added MariaDB 11 service with healthcheck, named volume, and default credentials.
- Both Dockerfiles (API + Worker) updated to use `src/` as build context to include `HashProcessing.Contracts` project reference.
- Compose build contexts updated from per-project to `src/` directory.

**Packages (Worker):**
- `RabbitMQ.Client` 7.2.1
- `Pomelo.EntityFrameworkCore.MySql` 8.0.3 (not 9.x — incompatible with .NET 8)
- `Microsoft.EntityFrameworkCore.Design` 8.0.12
- Project reference to `HashProcessing.Contracts`

**EF Migration:**
- `InitialCreate` migration generated — creates `hashes` table (`id` varchar(36) PK, `date` date indexed, `sha1` varchar(40)).
- Installed `dotnet-ef` 8.0.24 globally.

### 4. Copilot instructions update
- Added `## Dependencies` section to `.github/copilot-instructions.md` — NuGet versions must be compatible with .NET 8.0.

### 5. README updated
- Reflects Worker architecture, MariaDB, anti-corruption layer, 4 parallel consumers, updated project structure and tech stack.

## Verified end-to-end
- `POST /hashes` → 202 Accepted → 40,000 hashes published (400 batches × 100) → consumed by 4 parallel workers → persisted to MariaDB → queue drained to 0.
- `SELECT COUNT(*) FROM hashes` = 40,000.
- All existing tests still pass.
