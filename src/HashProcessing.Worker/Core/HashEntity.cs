namespace HashProcessing.Worker.Core;

public class HashEntity
{
    public HashEntity(string id, DateOnly date, string sha1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha1);

        Id = id;
        Date = date;
        Sha1 = sha1;
    }

    public string Id { get; private set; }
    public DateOnly Date { get; private set; }
    public string Sha1 { get; private set; }
}
