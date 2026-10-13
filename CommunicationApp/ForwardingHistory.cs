using System.Text.Json;
using Shared;

namespace CommunicationApp;

public static class ForwardingHistory
{
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
        Add(HistoryEntries.Forwarded(messageText, DateTime.Now));
    }

    public static void AddFailure(string error)
    {
        lock (Sync)
        {
            // While the server stays unreachable every check fails, so keep only the first failure.
            if (HistoryEntries.LastIsFailure(Load()))
                return;
        }

        Add(HistoryEntries.Failure(error, DateTime.Now));
    }

    private static void Add(HistoryEntry entry)
    {
        lock (Sync)
        {
            var entries = Load();
            HistoryEntries.AddNewest(entries, entry);

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
