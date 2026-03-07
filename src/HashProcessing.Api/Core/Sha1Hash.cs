using System.Text.RegularExpressions;

namespace HashProcessing.Api.Core;

public partial record Sha1Hash : IHash
{
    public Sha1Hash(string Value)
    {
        if(string.IsNullOrWhiteSpace(Value))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(Value));

        if (!Sha1Regex().IsMatch(Value))
            throw new ArgumentException("Value must be a valid 40-character hexadecimal SHA-1 string.", nameof(Value));

        this.Value = Value.ToLowerInvariant();
    }

    public string Value { get; init; }

    public void Deconstruct(out string value)
    {
        value = Value;
    }

    [GeneratedRegex(@"\A[0-9a-fA-F]{40}\z")]
    private static partial Regex Sha1Regex();
}