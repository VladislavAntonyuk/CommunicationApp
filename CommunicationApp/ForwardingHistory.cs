using System.Text.Json;

namespace CommunicationApp;

public record HistoryEntry(DateTime Time, string Source, string Text);

public static class ForwardingHistory
{
    private const int MaxEntries = 100;
    private const string FailedSource = "Failed";

    private static readonly string FilePath = Path.Combine(FileSystem.AppDataDirectory, "forwarding-history.json");
    private static readonly object Sync = new();
    private static List<HistoryEntry>? _entries;

    public static event Action? Changed;

    public static List<HistoryEntry> GetEntries()
    {
        lock (Sync)
        {
            return [.. Load()];
        }
    }

    public static void AddForwarded(string messageText)
    {
        // Messages arrive as Source|sender|text.
        var parts = messageText.Split('|', 3);
        var entry = parts.Length == 3
            ? new HistoryEntry(DateTime.Now, parts[0], $"{parts[1]}: {parts[2]}")
            : new HistoryEntry(DateTime.Now, "Message", messageText);
        Add(entry);
    }

    public static void AddFailure(string error)
    {
        lock (Sync)
        {
            // While the server stays unreachable every check fails, so keep only the first failure.
            if (Load().FirstOrDefault()?.Source == FailedSource)
                return;
        }

        Add(new HistoryEntry(DateTime.Now, FailedSource, error));
    }

    private static void Add(HistoryEntry entry)
    {
        lock (Sync)
        {
            var entries = Load();
            entries.Insert(0, entry);
            if (entries.Count > MaxEntries)
                entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);

            try
            {
                File.WriteAllText(FilePath, JsonSerializer.Serialize(entries));
            }
            catch (IOException)
            {
            }
        }

        Changed?.Invoke();
    }

    private static List<HistoryEntry> Load()
    {
        if (_entries != null)
            return _entries;

        try
        {
            _entries = File.Exists(FilePath)
                ? JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(FilePath)) ?? []
                : [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _entries = [];
        }

        return _entries;
    }
}
