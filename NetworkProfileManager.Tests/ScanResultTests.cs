using NetworkProfileManager.Models;

namespace NetworkProfileManager.Tests;

/// <summary>
/// Display-layer contract of a scan row. These labels are what the operator reads
/// in the grid and in exported CSV, so they must not imply more than was measured.
/// </summary>
public class ScanResultTests
{
    [Theory]
    [InlineData(DiscoveryMethod.Icmp, 0, "ICMP")]
    [InlineData(DiscoveryMethod.Arp,  0, "ARP")]
    [InlineData(DiscoveryMethod.None, 0, "")]
    public void FoundByLabel_MapsMethod(DiscoveryMethod method, int port, string expected)
    {
        var r = new ScanResult { FoundBy = method, OpenPort = port };
        Assert.Equal(expected, r.FoundByLabel);
    }

    [Fact]
    public void FoundByLabel_IncludesPort_ForTcpDiscovery()
    {
        var r = new ScanResult { FoundBy = DiscoveryMethod.Tcp, OpenPort = 502 };
        Assert.Equal("TCP:502", r.FoundByLabel);
    }

    [Fact]
    public void ResponseTimeLabel_IsBlank_WhenHostNeverAnsweredIcmp()
    {
        // An ARP-only host has no latency measurement; showing "-1" would be a lie.
        var r = new ScanResult { IsOnline = true, FoundBy = DiscoveryMethod.Arp };
        Assert.Equal(-1, r.ResponseTime);
        Assert.Equal("", r.ResponseTimeLabel);
    }

    [Fact]
    public void ResponseTimeLabel_ShowsZero_ForSubMillisecondReply()
    {
        var r = new ScanResult { ResponseTime = 0 };
        Assert.Equal("0", r.ResponseTimeLabel);
    }

    [Fact]
    public void ScanOptions_DefaultsFavourCompleteness()
    {
        var o = new ScanOptions();
        Assert.True(o.UseArp,      "ARP is what finds ICMP-filtered hosts; it must default on");
        Assert.True(o.UseTcpProbe, "TCP probe is what reaches off-subnet hosts");
        Assert.True(o.IcmpRetries >= 1, "weak devices drop the odd echo under a sweep");
        Assert.True(o.TimeoutMs >= 1000, "500 ms is too aggressive for old industrial gear");
    }
}
