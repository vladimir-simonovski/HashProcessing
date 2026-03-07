namespace HashProcessing.Api.Core;

public interface IGeneratedHash
{
    string Id { get; }
    DateTimeOffset Date { get; }
    string Value { get; }
}