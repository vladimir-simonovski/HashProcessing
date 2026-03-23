namespace HashProcessing.Worker.Application;

public interface IDailyHashCountNotifier
{
    Task NotifyDailyHashCountsAsync(IReadOnlyDictionary<DateOnly, long> countsByDate, CancellationToken ct = default);
}
