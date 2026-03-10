namespace HashProcessing.Messaging;

public abstract record MessageBase
{
    public string Id { get; } = Guid.NewGuid().ToString();
}