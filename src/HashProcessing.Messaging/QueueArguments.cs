namespace HashProcessing.Messaging;

public record QueueArguments
{
    public string DeadLetterExchange { get; init; } = "dlx";
    
    public IDictionary<string, object?> ToDictionary() =>
        new Dictionary<string, object?>
        {
            { "x-dead-letter-exchange", DeadLetterExchange }
        };
}