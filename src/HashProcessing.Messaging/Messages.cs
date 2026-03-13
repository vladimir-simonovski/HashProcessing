namespace HashProcessing.Messaging;

public abstract record MessageBase
{
    public string Id { get; } = Guid.NewGuid().ToString();
}

public record HashBatchMessageBase(params Hash[] Hashes) : MessageBase;
public record Hash(string Id, DateTimeOffset Date, string Value);

public record HashDailyCountMessageBase(DateOnly Date, long Count) : MessageBase;
