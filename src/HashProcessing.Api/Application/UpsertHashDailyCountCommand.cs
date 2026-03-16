using HashProcessing.Api.Core;

namespace HashProcessing.Api.Application;

public record UpsertHashDailyCountCommand(DateOnly Date, long Count);

public class UpsertHashDailyCountCommandHandler(IHashDailyCountRepository repository)
{
    private readonly IHashDailyCountRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public Task HandleAsync(UpsertHashDailyCountCommand command, CancellationToken ct = default) 
        => _repository.UpsertAsync(command.Date, command.Count, ct);
}
