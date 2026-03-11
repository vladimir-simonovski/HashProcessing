namespace HashProcessing.Api.Core;

public class HashDailyCount
{
    // ReSharper disable once UnusedMember.Local
    private HashDailyCount() { } // EF Core

    public HashDailyCount(DateOnly date, long count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        Date = date;
        Count = count;
    }

    public DateOnly Date { get; private set; }
    public long Count { get; private set; }
}
