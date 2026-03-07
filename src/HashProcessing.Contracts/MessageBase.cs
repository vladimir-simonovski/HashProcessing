namespace HashProcessing.Contracts;

public abstract record MessageBase
{
    public string Id { get; } = Guid.NewGuid().ToString();
}