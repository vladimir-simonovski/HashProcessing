namespace HashProcessing.Worker.Application;

public class WorkerOptions
{
    public string PublishQueueName { get; init; } = "hash-daily-counts";
    public string DeadLetterExchange { get; init; } = "dlx";
}
