using NetworkProfileManager.Helpers;

namespace NetworkProfileManager.Tests;

/// <summary>
/// NotificationService.Show* methods construct Wpf.Ui SymbolIcon which requires
/// STA + a live WPF Application — out of scope for unit tests. Here we only
/// verify the TrayFallback property surface.
/// </summary>
[Collection("StaticState")]
public class NotificationServiceTests
{
    [Fact]
    public void TrayFallback_CanBeSetAndCleared()
    {
        var prev = NotificationService.TrayFallback;
        try
        {
            NotificationService.TrayFallback = (_, _) => { };
            Assert.NotNull(NotificationService.TrayFallback);

            NotificationService.TrayFallback = null;
            Assert.Null(NotificationService.TrayFallback);
        }
        finally { NotificationService.TrayFallback = prev; }
    }
}
