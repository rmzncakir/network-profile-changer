using NetworkProfileManager.Helpers;
using Xunit;

namespace NetworkProfileManager.Tests;

public class IpFormatTests
{
    [Theory]
    [InlineData("255.255.255.0",   24)]
    [InlineData("255.255.254.0",   23)]
    [InlineData("255.255.255.255", 32)]
    [InlineData("255.0.0.0",       8)]
    [InlineData("0.0.0.0",         0)]
    [InlineData(" 255.255.255.128 ", 25)]
    public void PrefixLength_ValidMask(string mask, int expected)
        => Assert.Equal(expected, IpFormat.PrefixLength(mask));

    [Theory]
    [InlineData("255.0.255.0")]      // not contiguous
    [InlineData("255.255.255.1")]
    [InlineData("0.255.255.255")]
    [InlineData("not a mask")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("::1")]              // IPv6 is not a subnet mask here
    public void PrefixLength_InvalidMask_ReturnsNull(string? mask)
        => Assert.Null(IpFormat.PrefixLength(mask));

    [Fact]
    public void WithPrefix_UsesCidrForValidMask()
        => Assert.Equal("192.168.0.10/24", IpFormat.WithPrefix("192.168.0.10", "255.255.255.0"));

    [Fact]
    public void WithPrefix_KeepsOddMaskVisible()
        => Assert.Equal("10.0.0.5 / 255.0.255.0", IpFormat.WithPrefix("10.0.0.5", "255.0.255.0"));

    [Fact]
    public void WithPrefix_NoMask_ReturnsIpOnly()
        => Assert.Equal("10.0.0.5", IpFormat.WithPrefix("10.0.0.5", ""));
}
