namespace Shared;

public record ReceivedMessage(string Id, string Text);

public record Acknowledgement(List<string> Ids);
