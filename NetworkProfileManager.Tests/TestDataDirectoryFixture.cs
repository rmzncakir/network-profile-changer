using System;
using System.IO;
using NetworkProfileManager.Helpers;

namespace NetworkProfileManager.Tests;

/// <summary>
/// Redirects AppPaths.DataDirectory to a unique temp folder for the lifetime of a test class.
/// Avoids polluting %LOCALAPPDATA%\NetworkProfileManager during test runs.
/// </summary>
public sealed class TestDataDirectoryFixture : IDisposable
{
    public string Root { get; }

    public TestDataDirectoryFixture()
    {
        Root = Path.Combine(Path.GetTempPath(),
            $"NetworkProfileManager.Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
        AppPaths.OverrideDataDirectory(Root);
    }

    public void Dispose()
    {
        AppPaths.OverrideDataDirectory(null);
        try { Directory.Delete(Root, recursive: true); } catch { }
    }
}

[CollectionDefinition("StaticState", DisableParallelization = true)]
public class StaticStateCollection { }
