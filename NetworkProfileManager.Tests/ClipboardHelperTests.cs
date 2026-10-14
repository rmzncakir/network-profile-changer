using System;
using System.Runtime.InteropServices;
using NetworkProfileManager.Helpers;

namespace NetworkProfileManager.Tests;

/// <summary>
/// A busy clipboard must not crash the app: the copy buttons go through
/// ClipboardHelper, which reports failure instead of throwing.
/// </summary>
public class ClipboardHelperTests
{
    private const int ClipboardCantOpen = unchecked((int)0x800401D0);   // CLIPBRD_E_CANT_OPEN

    [Fact]
    public void ClipboardHeldByAnotherProcess_ReturnsFalse()
        => Assert.False(ClipboardHelper.TrySetText("192.168.0.1",
            _ => throw new COMException("OpenClipboard failed", ClipboardCantOpen)));

    [Fact]
    public void Success_ReturnsTrueAndCopies()
    {
        string? copied = null;
        Assert.True(ClipboardHelper.TrySetText("192.168.0.1", t => copied = t));
        Assert.Equal("192.168.0.1", copied);
    }

    [Fact]
    public void UnrelatedErrors_AreNotSwallowed()
        => Assert.Throws<InvalidOperationException>(() =>
            ClipboardHelper.TrySetText("x", _ => throw new InvalidOperationException()));
}
