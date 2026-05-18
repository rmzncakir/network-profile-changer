using System;
using NetworkProfileManager.Models;
using NetworkProfileManager.Services;

namespace NetworkProfileManager.Tests;

[Collection("StaticState")]
public class HistoryRepositoryTests : IDisposable
{
    private readonly TestDataDirectoryFixture _dir = new();
    public void Dispose() => _dir.Dispose();

    private static string NewId() => $"test-{Guid.NewGuid():N}";

    [Fact]
    public void Load_ReturnsEmpty_WhenNoFile()
    {
        var repo = new HistoryRepository();
        var entries = repo.Load(NewId());
        Assert.NotNull(entries);
        Assert.Empty(entries);
    }

    [Fact]
    public void Append_PrependsNewEntries()
    {
        var repo = new HistoryRepository();
        var id = NewId();

        repo.Append(id, new HistoryEntry { ProfileName = "First",  Timestamp = DateTime.Now.AddMinutes(-2) });
        repo.Append(id, new HistoryEntry { ProfileName = "Second", Timestamp = DateTime.Now.AddMinutes(-1) });
        repo.Append(id, new HistoryEntry { ProfileName = "Third",  Timestamp = DateTime.Now });

        var loaded = repo.Load(id);

        Assert.Equal(3, loaded.Count);
        Assert.Equal("Third",  loaded[0].ProfileName);
        Assert.Equal("Second", loaded[1].ProfileName);
        Assert.Equal("First",  loaded[2].ProfileName);
    }

    [Fact]
    public void Append_CapsAt100Entries()
    {
        var repo = new HistoryRepository();
        var id = NewId();

        for (int i = 0; i < 110; i++)
            repo.Append(id, new HistoryEntry { ProfileName = $"E{i}" });

        var loaded = repo.Load(id);

        Assert.Equal(100, loaded.Count);
        Assert.Equal("E109", loaded[0].ProfileName);
    }

    [Fact]
    public void Clear_RemovesFile()
    {
        var repo = new HistoryRepository();
        var id = NewId();
        repo.Append(id, new HistoryEntry { ProfileName = "X" });
        Assert.Single(repo.Load(id));

        repo.Clear(id);

        Assert.Empty(repo.Load(id));
    }
}
