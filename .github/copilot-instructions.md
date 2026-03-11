# HashProcessing

## Stack
- .NET 8 (C# with nullable reference types and implicit usings enabled)
- ASP.NET Core Minimal API
- RabbitMQ (via `RabbitMQ.Client`)
- Docker & Docker Compose
- xUnit + NSubstitute for testing

## Architecture
- Follow **Clean Architecture** with inward-only dependency flow: `Core → Application → Infrastructure`.
- **Core** contains interfaces (`IHash`, `IHashGenerator`, `IHashProcessor`) and value objects (`Sha1Hash`). No dependencies on external packages.
- **Application** contains CQRS command/handler pairs (e.g., `GenerateHashesCommand` / `GenerateHashesCommandHandler`). Depends only on Core.
- **Infrastructure** contains concrete implementations and third-party integrations. Depends on Core.
- Register services via `ServiceCollectionExtensions` classes — one per layer (`Application`, `Infrastructure`).
- **Message consumers are anti-corruption layers** — they translate external messages into Application-layer commands/queries and delegate to the corresponding handler. Consumers must never call repositories or execute business logic directly.

## Dependencies
- When adding NuGet packages, always choose the latest version compatible with the target .NET version (currently .NET 8.0). Do not install major versions that require a newer runtime (e.g., avoid EF Core 9.x / Pomelo 9.x on a .NET 8 project).
- Always pin NuGet package versions to a specific version number (e.g., `8.10.0`), never use floating versions (e.g., `8.*`). This ensures reproducible builds and prevents unexpected breaking changes from automatic version resolution.

## Conventions
- Guard constructor arguments against null/empty/invalid values.
- Use `System.Threading.Channels` for streaming data between producer and consumer stages.
- Use `record` or `record struct` for value objects and DTOs.
- Validate domain invariants in constructors (throw `ArgumentException`/`ArgumentNullException`/`ArgumentOutOfRangeException` and other relevant exceptions as needed to guard invariants).
- Use file-scoped namespaces.
- Name test classes `<ClassUnderTest>Should` and test methods descriptively - either using the `MethodName_WhenStateUnderTest_ExpectedBehavior` pattern (e.g. `GenerateHashesCommandHandler_WhenValidRequest_ProcessesHashes`) or `MethodName_ExpectedBehavior` pattern for simpler cases (e.g., `ProcessAsync_ReturnsCorrectCountResults`).

## Documentation
- Always keep documentation in markdown files clear, concise, and purely technical. Avoid including any personal opinions or subjective statements in the documentation.
- Always keep [README.md](README.md) up to date and update it whenever you add new features or make significant changes to the project.