using System;
using NetworkProfileManager.Services;

namespace NetworkProfileManager.Tests;

/// <summary>
/// Defense-in-depth tests for NetworkService input validation.
/// These guard against netsh argument injection and malformed-input bugs.
/// We never invoke netsh itself in CI — only the validation surface.
/// </summary>
public class NetworkServiceValidationTests
{
    // ── EnsureValidAdapterName ─────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void EnsureValidAdapterName_RejectsEmptyOrWhitespace(string? name)
    {
        Assert.Throws<ArgumentException>(() => NetworkService.EnsureValidAdapterName(name!));
    }

    [Theory]
    [InlineData("Ethernet\"")]                  // embedded double-quote
    [InlineData("Wi-Fi\nexec C:\\evil.bat")]    // newline injection
    [InlineData("Adapter\r\nmalicious")]        // CRLF
    [InlineData("Name\0Null")]                  // null byte
    [InlineData("With\x01control")]             // control char
    [InlineData("Tab\there")]                   // control: tab
    public void EnsureValidAdapterName_RejectsDangerousCharacters(string name)
    {
        var ex = Assert.Throws<ArgumentException>(() => NetworkService.EnsureValidAdapterName(name));
        Assert.Contains("geçersiz karakter", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureValidAdapterName_RejectsExcessiveLength()
    {
        var huge = new string('A', 257);
        Assert.Throws<ArgumentException>(() => NetworkService.EnsureValidAdapterName(huge));
    }

    [Theory]
    [InlineData("Ethernet")]
    [InlineData("Wi-Fi")]
    [InlineData("Ethernet 2")]
    [InlineData("Ağ Bağlantısı")]
    [InlineData("Local Area Connection")]
    [InlineData("vEthernet (Default Switch)")]
    public void EnsureValidAdapterName_AcceptsRealisticNames(string name)
    {
        var ex = Record.Exception(() => NetworkService.EnsureValidAdapterName(name));
        Assert.Null(ex);
    }

    // ── EnsureValidIp ──────────────────────────────────────────────

    [Theory]
    [InlineData("192.168.1.1")]
    [InlineData("10.0.0.0")]
    [InlineData("255.255.255.0")]
    [InlineData("0.0.0.0")]
    [InlineData("8.8.8.8")]
    public void EnsureValidIp_AcceptsValidIPv4(string ip)
    {
        var ex = Record.Exception(() => NetworkService.EnsureValidIp(ip, "ip"));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-ip")]
    [InlineData("256.0.0.1")]
    [InlineData("192.168.1")]               // legacy shorthand — must reject
    [InlineData("192.168.1.1.1")]
    [InlineData("192.168.1.-1")]
    [InlineData("192.168.1.1; evil")]
    [InlineData("192.168.1.1\nmalicious")]
    public void EnsureValidIp_RejectsInvalidStrings(string ip)
    {
        Assert.Throws<ArgumentException>(() => NetworkService.EnsureValidIp(ip, "ip"));
    }

    [Theory]
    [InlineData("::1")]
    [InlineData("2001:db8::1")]
    [InlineData("fe80::1")]
    public void EnsureValidIp_RejectsIPv6(string ip)
    {
        Assert.Throws<ArgumentException>(() => NetworkService.EnsureValidIp(ip, "ip"));
    }

    // ── ApplyDns short-circuit ─────────────────────────────────────

    [Fact]
    public void ApplyDns_NoOpWhenPrimaryEmpty()
    {
        var svc = new NetworkService();
        var ex = Record.Exception(() => svc.ApplyDns("Ethernet", "", ""));
        Assert.Null(ex);
    }

    [Fact]
    public void ApplyDns_ValidatesPrimaryBeforeAdapterUse()
    {
        var svc = new NetworkService();
        Assert.Throws<ArgumentException>(() => svc.ApplyDns("Ethernet", "not-an-ip"));
    }

    // ── End-to-end argument validation (throws before netsh would run) ──

    [Fact]
    public void ApplyStaticIp_RejectsInjectedAdapterName()
    {
        var svc = new NetworkService();
        Assert.Throws<ArgumentException>(() =>
            svc.ApplyStaticIp("Eth\"\nexec C:\\bad.bat", "192.168.1.10", "255.255.255.0", "192.168.1.1"));
    }

    [Fact]
    public void ApplyStaticIp_RejectsInvalidIp()
    {
        var svc = new NetworkService();
        Assert.Throws<ArgumentException>(() =>
            svc.ApplyStaticIp("Ethernet", "999.999.999.999", "255.255.255.0", "192.168.1.1"));
    }

    [Fact]
    public void ApplyStaticIp_RejectsInvalidSubnet()
    {
        var svc = new NetworkService();
        Assert.Throws<ArgumentException>(() =>
            svc.ApplyStaticIp("Ethernet", "192.168.1.10", "garbage", "192.168.1.1"));
    }

    [Fact]
    public void ApplyStaticIp_RejectsInvalidGateway()
    {
        var svc = new NetworkService();
        Assert.Throws<ArgumentException>(() =>
            svc.ApplyStaticIp("Ethernet", "192.168.1.10", "255.255.255.0", "999.0.0.0"));
    }

    [Fact]
    public void ApplyDhcp_RejectsInjectedAdapterName()
    {
        var svc = new NetworkService();
        Assert.Throws<ArgumentException>(() => svc.ApplyDhcp("Eth\nrm -rf /"));
    }
}
