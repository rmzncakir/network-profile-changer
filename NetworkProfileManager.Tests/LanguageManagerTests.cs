using System.Globalization;
using NetworkProfileManager.Helpers;

namespace NetworkProfileManager.Tests;

/// <summary>
/// LanguageManager swaps ResourceDictionaries on Application.Current, which is
/// null in unit tests. We test only the parts that don't require a live WPF app:
/// system-language resolution and the preference normalization contract.
/// </summary>
[Collection("StaticState")]
public class LanguageManagerTests
{
    [Theory]
    [InlineData(LanguageManager.Turkish, LanguageManager.Turkish)]
    [InlineData(LanguageManager.English, LanguageManager.English)]
    public void ResolveEffective_PassThroughForExplicitChoice(string pref, string expected)
    {
        Assert.Equal(expected, LanguageManager.ResolveEffective(pref));
    }

    [Fact]
    public void ResolveEffective_System_FollowsDetectorTurkish()
    {
        var saved = LanguageManager.SystemLanguageDetector;
        try
        {
            LanguageManager.SystemLanguageDetector = () => LanguageManager.Turkish;
            Assert.Equal(LanguageManager.Turkish,
                LanguageManager.ResolveEffective(LanguageManager.SystemDefault));
        }
        finally { LanguageManager.SystemLanguageDetector = saved; }
    }

    [Fact]
    public void ResolveEffective_System_FollowsDetectorEnglish()
    {
        var saved = LanguageManager.SystemLanguageDetector;
        try
        {
            LanguageManager.SystemLanguageDetector = () => LanguageManager.English;
            Assert.Equal(LanguageManager.English,
                LanguageManager.ResolveEffective(LanguageManager.SystemDefault));
        }
        finally { LanguageManager.SystemLanguageDetector = saved; }
    }

    [Fact]
    public void ResolveEffective_System_DoesNotFollowThreadCurrentUICulture()
    {
        // Bug guard: previously CurrentUICulture was used. After ApplyPreference
        // overrides the thread culture, "System" must still reflect the actual OS.
        var saved = LanguageManager.SystemLanguageDetector;
        var savedCulture = CultureInfo.CurrentUICulture;
        try
        {
            LanguageManager.SystemLanguageDetector = () => LanguageManager.Turkish;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal(LanguageManager.Turkish,
                LanguageManager.ResolveEffective(LanguageManager.SystemDefault));
        }
        finally
        {
            LanguageManager.SystemLanguageDetector = saved;
            CultureInfo.CurrentUICulture = savedCulture;
        }
    }

    [Fact]
    public void ResolveEffective_UnknownPreference_TreatedAsSystem()
    {
        var saved = LanguageManager.SystemLanguageDetector;
        try
        {
            LanguageManager.SystemLanguageDetector = () => LanguageManager.English;
            Assert.Equal(LanguageManager.English,
                LanguageManager.ResolveEffective("garbage"));
        }
        finally { LanguageManager.SystemLanguageDetector = saved; }
    }

    [Fact]
    public void Constants_ExposeCanonicalCodes()
    {
        Assert.Equal("tr",     LanguageManager.Turkish);
        Assert.Equal("en",     LanguageManager.English);
        Assert.Equal("System", LanguageManager.SystemDefault);
    }
}

[Collection("StaticState")]
public class LocTests
{
    [Fact]
    public void Get_ReturnsFallback_WhenApplicationNotRunning()
    {
        // Application.Current is null in xUnit; Loc must not blow up.
        var result = Loc.Get("Some.Missing.Key", "fallback-value");
        Assert.Equal("fallback-value", result);
    }

    [Fact]
    public void Get_ReturnsKey_WhenFallbackNull()
    {
        var result = Loc.Get("Some.Missing.Key");
        Assert.Equal("Some.Missing.Key", result);
    }

    [Fact]
    public void Format_AppliesArgumentsToFallback()
    {
        var result = Loc.Format("Some.Missing.Key.{0}", 42);
        // When key is missing the fallback IS the key string; if it has a {0} placeholder
        // string.Format will fill it
        Assert.Contains("42", result);
    }

    [Fact]
    public void Format_DoesNotThrow_OnMalformedFormatString()
    {
        // Fallback "{not-a-number}" has invalid placeholder syntax — Loc.Format swallows
        var ex = Record.Exception(() => Loc.Format("Some.Key.With.{garbage}"));
        Assert.Null(ex);
    }
}
