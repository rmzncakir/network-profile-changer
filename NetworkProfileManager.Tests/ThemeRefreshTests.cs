using Wpf.Ui.Appearance;

namespace NetworkProfileManager.Tests;

/// <summary>
/// "System" is the default theme, so a Windows light/dark switch has to reach
/// the running app. The decision is kept pure so it can be checked without a
/// live Windows theme change.
/// </summary>
public class ThemeRefreshTests
{
    [Theory]
    [InlineData(ApplicationTheme.Light, SystemTheme.Dark)]
    [InlineData(ApplicationTheme.Dark,  SystemTheme.Light)]
    [InlineData(ApplicationTheme.Light, SystemTheme.Glow)]            // dark-based Windows theme
    [InlineData(ApplicationTheme.Dark,  SystemTheme.Sunrise)]         // light-based Windows theme
    [InlineData(ApplicationTheme.Light, SystemTheme.HCBlack)]
    public void System_FollowsWindowsWhenTheyDiffer(ApplicationTheme showing, SystemTheme windows)
        => Assert.True(App.NeedsThemeRefresh("System", showing, windows, accentChanged: false));

    [Theory]
    [InlineData(ApplicationTheme.Light, SystemTheme.Light)]
    [InlineData(ApplicationTheme.Dark,  SystemTheme.Dark)]
    [InlineData(ApplicationTheme.Dark,  SystemTheme.CapturedMotion)]
    public void System_NoRefreshWhenAlreadyMatching(ApplicationTheme showing, SystemTheme windows)
        => Assert.False(App.NeedsThemeRefresh("System", showing, windows, accentChanged: false));

    [Theory]
    [InlineData("Light", ApplicationTheme.Light, SystemTheme.Dark)]
    [InlineData("Light", ApplicationTheme.Light, SystemTheme.Light)]
    [InlineData("Dark",  ApplicationTheme.Dark,  SystemTheme.Light)]
    [InlineData("Dark",  ApplicationTheme.Dark,  SystemTheme.Dark)]
    public void PinnedTheme_IgnoresWindowsLightDark(string pinned, ApplicationTheme showing, SystemTheme windows)
        => Assert.False(App.NeedsThemeRefresh(pinned, showing, windows, accentChanged: false));

    [Theory]
    [InlineData("System")]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void AccentChange_AlwaysRefreshes(string theme)
        => Assert.True(App.NeedsThemeRefresh(theme, ApplicationTheme.Light, SystemTheme.Light, accentChanged: true));

    [Theory]
    [InlineData("Light",    "Light")]
    [InlineData("Dark",     "Dark")]
    [InlineData("DarkBlue", "Dark")]     // retired palette
    [InlineData("System",   "System")]
    [InlineData(null,       "System")]   // nothing saved yet
    [InlineData("Sepia",    "System")]
    public void NormalizeTheme_MapsSavedValues(string? saved, string expected)
        => Assert.Equal(expected, App.NormalizeTheme(saved));
}
