using Shared;

namespace CommunicationApi.Tests;

public sealed class HistoryEntriesTests
{
    private static readonly DateTime Time = new(2026, 10, 13, 9, 30, 0);

    [Theory]
    [InlineData("Telegram|123|hello", "Telegram", "123: hello")]
    [InlineData("Slack|U1|one|two", "Slack", "U1: one|two")]
    [InlineData("Slack|U1|", "Slack", "U1: ")]
    [InlineData("Slack|U1", "Message", "Slack|U1")]
    [InlineData("hello", "Message", "hello")]
    [InlineData("", "Message", "")]
    public void Forwarded_SplitsMessageIntoSourceAndText(string messageText, string source, string text)
    {
        var entry = HistoryEntries.Forwarded(messageText, Time);

        Assert.Equal(new HistoryEntry(Time, source, text), entry);
    }

    [Fact]
    public void Failure_UsesFailedSource()
    {
        var entry = HistoryEntries.Failure("Could not reach the server", Time);

        Assert.Equal(new HistoryEntry(Time, HistoryEntries.FailedSource, "Could not reach the server"), entry);
    }

    [Fact]
    public void LastIsFailure_NoEntries_ReturnsFalse()
    {
        Assert.False(HistoryEntries.LastIsFailure([]));
    }

    [Fact]
    public void LastIsFailure_NewestIsFailure_ReturnsTrue()
    {
        List<HistoryEntry> entries = [];
        HistoryEntries.AddNewest(entries, HistoryEntries.Forwarded("Slack|U1|hello", Time));
        HistoryEntries.AddNewest(entries, HistoryEntries.Failure("error", Time));

        Assert.True(HistoryEntries.LastIsFailure(entries));
    }

    [Fact]
    public void LastIsFailure_ForwardedAfterFailure_ReturnsFalse()
    {
        List<HistoryEntry> entries = [];
        HistoryEntries.AddNewest(entries, HistoryEntries.Failure("error", Time));
        HistoryEntries.AddNewest(entries, HistoryEntries.Forwarded("Slack|U1|hello", Time));

        Assert.False(HistoryEntries.LastIsFailure(entries));
    }

    [Fact]
    public void AddNewest_PutsEntryFirst()
    {
        List<HistoryEntry> entries = [];

        HistoryEntries.AddNewest(entries, new HistoryEntry(Time, "Slack", "first"));
        HistoryEntries.AddNewest(entries, new HistoryEntry(Time, "Slack", "second"));

        Assert.Equal(new[] { "second", "first" }, entries.Select(x => x.Text).ToArray());
    }

    [Fact]
    public void AddNewest_OverLimit_DropsOldest()
    {
        List<HistoryEntry> entries = [];

        for (var i = 0; i <= HistoryEntries.MaxEntries; i++)
        {
            HistoryEntries.AddNewest(entries, new HistoryEntry(Time, "Slack", i.ToString()));
        }

        Assert.Equal(HistoryEntries.MaxEntries, entries.Count);
        Assert.Equal(HistoryEntries.MaxEntries.ToString(), entries[0].Text);
        Assert.Equal("1", entries[^1].Text);
    }
}
