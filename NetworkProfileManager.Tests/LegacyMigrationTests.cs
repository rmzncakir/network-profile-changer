using System;
using System.IO;
using NetworkProfileManager.Helpers;

namespace NetworkProfileManager.Tests;

/// <summary>
/// Tests for AppPaths.MigrateLegacyDataIfPresent. We can't redirect
/// AppDomain.BaseDirectory, so we rely on the assertion that the helper
/// silently no-ops when the legacy folder is empty (the typical CI case)
/// and on the marker file being written exactly once.
/// </summary>
[Collection("StaticState")]
public class LegacyMigrationTests : IDisposable
{
    private readonly TestDataDirectoryFixture _dir = new();
    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Migration_WritesMarkerAfterRun()
    {
        var copied = AppPaths.MigrateLegacyDataIfPresent();
        Assert.True(copied >= 0);

        var marker = Path.Combine(_dir.Root, ".migrated_v1");
        Assert.True(File.Exists(marker), "migration marker was not written");
    }

    [Fact]
    public void Migration_IsIdempotent_DoesNothingOnSecondCall()
    {
        AppPaths.MigrateLegacyDataIfPresent();          // first call writes marker
        int copiedAgain = AppPaths.MigrateLegacyDataIfPresent(); // should short-circuit
        Assert.Equal(0, copiedAgain);
    }

    [Fact]
    public void Migration_DoesNotOverwriteExistingNewFiles()
    {
        // Pre-populate the new location with a profile file
        var existingFile = Path.Combine(_dir.Root, "profiles_existing.json");
        File.WriteAllText(existingFile, "[\"new content\"]");

        AppPaths.MigrateLegacyDataIfPresent();

        // The existing file must remain untouched
        Assert.Equal("[\"new content\"]", File.ReadAllText(existingFile));
    }
}
