namespace Shared;

public record HistoryEntry(DateTime Time, string Source, string Text);

public static class HistoryEntries
{
    public const int MaxEntries = 100;
    public const string FailedSource = "Failed";

    public static HistoryEntry Forwarded(string messageText, DateTime time)
    {
        var parts = messageText.Split('|', 3);
        return parts.Length == 3
            ? new HistoryEntry(time, parts[0], $"{parts[1]}: {parts[2]}")
            : new HistoryEntry(time, "Message", messageText);
    }

    public static HistoryEntry Failure(string error, DateTime time) => new(time, FailedSource, error);

    public static bool LastIsFailure(List<HistoryEntry> entries) => entries.FirstOrDefault()?.Source == FailedSource;

    public static void AddNewest(List<HistoryEntry> entries, HistoryEntry entry)
    {
        entries.Insert(0, entry);
        if (entries.Count > MaxEntries)
            entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
    }
}
