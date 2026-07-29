using System.Linq;
using NetworkProfileManager.Models;
using NetworkProfileManager.Services;

namespace NetworkProfileManager.Tests;

/// <summary>
/// Presets are typed into a live adapter, so a malformed one would surface as a
/// confusing netsh failure in the field. Every shipped preset must be applyable.
/// </summary>
public class VendorPresetTests
{
    [Fact]
    public void AllPresets_HaveDistinctNames()
    {
        var names = VendorPreset.All.Select(p => p.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void StaticPresets_CarryAddressesNetworkServiceAccepts()
    {
        foreach (var p in VendorPreset.All.Where(p => !p.IsDhcp))
        {
            NetworkService.EnsureValidIp(p.IpAddress,  $"{p.Name}.Ip");
            NetworkService.EnsureValidIp(p.SubnetMask, $"{p.Name}.Mask");
            NetworkService.EnsureValidIp(p.Gateway,    $"{p.Name}.Gateway");
        }
    }

    [Fact]
    public void StaticPresets_PlaceHostInsideItsOwnGatewaySubnet()
    {
        // A preset whose host sits on a different /24 than its gateway would leave the
        // engineer with no route — the exact failure the preset exists to prevent.
        foreach (var p in VendorPreset.All.Where(p => !p.IsDhcp))
        {
            var host = p.IpAddress.Substring(0, p.IpAddress.LastIndexOf('.'));
            var gw   = p.Gateway.Substring(0, p.Gateway.LastIndexOf('.'));
            Assert.Equal(gw, host);
            Assert.Equal("255.255.255.0", p.SubnetMask);
        }
    }

    [Fact]
    public void DhcpPreset_LeavesAddressFieldsEmpty()
    {
        var dhcp = VendorPreset.All.Single(p => p.IsDhcp);
        Assert.Equal("", dhcp.IpAddress);
        Assert.Equal("DHCP", dhcp.Description);
    }

    [Fact]
    public void Description_ShowsSubnetForStaticPresets()
    {
        var siemens = VendorPreset.All.First(p => !p.IsDhcp);
        Assert.Contains(siemens.IpAddress, siemens.Description);
        Assert.Contains(siemens.SubnetMask, siemens.Description);
    }
}
