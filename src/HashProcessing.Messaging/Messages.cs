namespace HashProcessing.Messaging;

public abstract record MessageBase
{
    // ReSharper disable once UnusedMember.Global
    public string Id { get; } = Guid.NewGuid().ToString();
}

public record HashBatchMessage(params Hash[] Hashes) : MessageBase;
public record Hash(string Id, DateTimeOffset Date, string Value);

public record HashDailyCountMessage(DateOnly Date, long Count) : MessageBase;
