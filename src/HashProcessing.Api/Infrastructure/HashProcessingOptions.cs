namespace HashProcessing.Api.Infrastructure;

public class HashProcessingOptions
{
    // ReSharper disable PropertyCanBeMadeInitOnly.Global
    public ushort ChannelCapacity { get; set; } = 128;
    public ushort DegreeOfParallelism { get; set; }
    public ushort BatchSize { get; set; } = 1000;
    public string PublishQueueName { get; set; } = "hash-processing";
    public string ConsumeQueueName { get; set; } = "hash-daily-counts";
    public string DeadLetterExchange { get; set; } = "dlx";
    // ReSharper restore PropertyCanBeMadeInitOnly.Global
}
