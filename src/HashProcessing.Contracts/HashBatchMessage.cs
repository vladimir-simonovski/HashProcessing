namespace HashProcessing.Contracts;

public record HashBatchMessage(params Hash[] Hashes) : MessageBase;
public record Hash(string Id, DateTimeOffset Date, string Value);