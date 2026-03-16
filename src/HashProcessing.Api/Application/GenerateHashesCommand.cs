using HashProcessing.Api.Core;

namespace HashProcessing.Api.Application;

public class GenerateHashesCommand(uint? count = null)
{
    private const uint MaxCount = 1_000_000;
    
    public uint Count { get; } = count switch
    {
        0 => throw new ArgumentException("Count must be greater than zero.", nameof(count)),
        > MaxCount => throw new ArgumentOutOfRangeException(nameof(count), count,
            $"Count must not exceed {MaxCount:N0}."),
        _ => count ?? 40_000
    };
}

public class GenerateHashesCommandHandler(
    IHashGenerator generator,
    IHashProcessor processor,
    ILogger<GenerateHashesCommandHandler> logger)
{
    private readonly IHashGenerator _generator = generator ?? throw new ArgumentNullException(nameof(generator));
    private readonly IHashProcessor _processor = processor ?? throw new ArgumentNullException(nameof(processor));
    private readonly ILogger<GenerateHashesCommandHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    
    public async Task HandleAsync(GenerateHashesCommand command, CancellationToken cancellationToken)
    {
        var hashes = _generator.StreamSha1s(command.Count, cancellationToken);
        var result = await _processor.ProcessAsync(hashes, ct: cancellationToken);
        
        if (result.StreamedCount < command.Count)
            _logger.LogWarning("Streamed {StreamedCount} hashes, which is less than the requested {RequestedCount}.",
                result.StreamedCount, command.Count);
    }
}