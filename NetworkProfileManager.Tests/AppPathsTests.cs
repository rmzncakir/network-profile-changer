using System;
using System.IO;
using NetworkProfileManager.Helpers;

namespace NetworkProfileManager.Tests;

[Collection("StaticState")]
public class AppPathsTests
{
    [Fact]
    public void DataDirectory_DefaultsToLocalAppData()
    {
        AppPaths.OverrideDataDirectory(null);
        try
        {
            var dir = AppPaths.DataDirectory;
            var localAppData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            Assert.StartsWith(localAppData, dir, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("NetworkProfileManager", dir);
            Assert.EndsWith("data", dir);
            Assert.True(Directory.Exists(dir));
        }
        finally { AppPaths.OverrideDataDirectory(null); }
    }

    [Fact]
    public void OverrideDataDirectory_TakesEffect_AndCreatesFolder()
    {
        var temp = Path.Combine(Path.GetTempPath(),
            $"NPM_Override_{Guid.NewGuid():N}");
        try
        {
            AppPaths.OverrideDataDirectory(temp);
            var dir = AppPaths.DataDirectory;
            Assert.Equal(temp, dir);
            Assert.True(Directory.Exists(temp));
        }
        finally
        {
            AppPaths.OverrideDataDirectory(null);
            try { Directory.Delete(temp, recursive: true); } catch { }
        }
    }

    [Fact]
    public void OverrideDataDirectory_Null_RestoresDefault()
    {
        AppPaths.OverrideDataDirectory(@"C:\Temp\foo");
        AppPaths.OverrideDataDirectory(null);
        var dir = AppPaths.DataDirectory;
        Assert.DoesNotContain(@"C:\Temp\foo", dir);
    }
}
