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