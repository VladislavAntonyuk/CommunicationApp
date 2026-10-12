namespace Shared;

public record ReceivedMessage(int Id, string Text);

public record Acknowledgement(List<int> Ids);
