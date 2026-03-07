using System.Text.RegularExpressions;

namespace HashProcessing.Api.Core;

public partial record Sha1Hash : IGeneratedHash
{
    public Sha1Hash(string Value)
    {
        if(string.IsNullOrWhiteSpace(Value))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(Value));

        if (!Sha1Regex().IsMatch(Value))
            throw new ArgumentException("Value must be a valid 40-character hexadecimal SHA-1 string.", nameof(Value));

        Id = Guid.NewGuid().ToString();
        Date = DateTimeOffset.UtcNow;
        this.Value = Value.ToLowerInvariant();
    }

    public string Id { get; }
    public DateTimeOffset Date { get; }
    public string Value { get; }

    [GeneratedRegex(@"\A[0-9a-fA-F]{40}\z")]
    private static partial Regex Sha1Regex();
}