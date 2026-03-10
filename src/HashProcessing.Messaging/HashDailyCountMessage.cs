namespace HashProcessing.Messaging;

public record HashDailyCountMessage(DateOnly Date, long Count) : MessageBase;
