using System;
using System.Collections.Generic;
using System.IO;
using NetworkProfileManager.Models;
using NetworkProfileManager.Services;

namespace NetworkProfileManager.Tests;

[Collection("StaticState")]
public class ProfileRepositoryTests : IDisposable
{
    private readonly TestDataDirectoryFixture _dir = new();
    public void Dispose() => _dir.Dispose();

    private static string NewId() => $"test-{Guid.NewGuid():N}";

    [Fact]
    public void Load_ReturnsEmpty_WhenNoFileExists()
    {
        var repo = new ProfileRepository();
        var profiles = repo.Load(NewId());
        Assert.NotNull(profiles);
        Assert.Empty(profiles);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsProfiles()
    {
        var repo = new ProfileRepository();
        var id = NewId();
        var input = new List<IpProfile>
        {
            new() { Name = "Home",   IpAddress = "192.168.1.10", SubnetMask = "255.255.255.0",
                    Gateway = "192.168.1.1", PrimaryDns = "8.8.8.8", SecondaryDns = "1.1.1.1" },
            new() { Name = "Office", IpAddress = "10.0.0.5",     SubnetMask = "255.255.0.0",
                    Gateway = "10.0.0.1" }
        };

        repo.Save(id, input);
        var loaded = repo.Load(id);

        Assert.Equal(2, loaded.Count);
        Assert.Equal("Home",         loaded[0].Name);
        Assert.Equal("192.168.1.10", loaded[0].IpAddress);
        Assert.Equal("8.8.8.8",      loaded[0].PrimaryDns);
        Assert.Equal("Office",       loaded[1].Name);
    }

    [Fact]
    public void Save_ThrowsWhenExceedingFiveProfiles()
    {
        var repo = new ProfileRepository();
        var six = new List<IpProfile>();
        for (int i = 0; i < 6; i++)
            six.Add(new IpProfile
            {
                Name = $"P{i}", IpAddress = $"10.0.0.{i + 1}",
                SubnetMask = "255.0.0.0", Gateway = "10.0.0.254"
            });

        var ex = Assert.Throws<InvalidOperationException>(() => repo.Save(NewId(), six));
        Assert.Contains("5 profil", ex.Message);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\..\\Windows\\System32")]
    [InlineData("name with spaces")]
    [InlineData("name:with:colons")]
    [InlineData("name/with/slash")]
    public void Save_SanitizesAdapterIdToPreventPathTraversal(string maliciousId)
    {
        var repo = new ProfileRepository();
        var profiles = new List<IpProfile>
        {
            new() { Name = "X", IpAddress = "1.1.1.1", SubnetMask = "255.255.255.0", Gateway = "1.1.1.1" }
        };

        repo.Save(maliciousId, profiles);

        Assert.True(Directory.Exists(_dir.Root));

        var parentOfRoot = Directory.GetParent(_dir.Root)!.FullName;
        var leakedOutside = Directory.EnumerateFiles(parentOfRoot, "passwd*", SearchOption.TopDirectoryOnly);
        Assert.Empty(leakedOutside);

        var matches = Directory.EnumerateFiles(_dir.Root, "profiles_*.json");
        Assert.NotEmpty(matches);
    }

    [Fact]
    public void Load_BacksUpAndResets_WhenJsonCorrupted()
    {
        var repo = new ProfileRepository();
        var id = NewId();
        var file = Path.Combine(_dir.Root, $"profiles_{id}.json");
        File.WriteAllText(file, "{ this is not valid json");

        var loaded = repo.Load(id);

        Assert.Empty(loaded);
        Assert.True(File.Exists(file + ".bak"));
        Assert.False(File.Exists(file));
    }
}
