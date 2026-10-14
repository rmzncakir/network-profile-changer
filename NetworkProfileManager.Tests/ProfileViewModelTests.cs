using System.Collections.ObjectModel;
using NetworkProfileManager.Models;
using NetworkProfileManager.ViewModels;

namespace NetworkProfileManager.Tests;

/// <summary>
/// "Active" decides what the profile row shows, and Ctrl+N applies a profile by
/// position, so both must describe the adapter and the list exactly.
/// </summary>
public class ProfileViewModelTests
{
    private static AdapterInfo StaticAdapter(string dns = "") => new()
    {
        Id = "1", Name = "Ethernet", IsConnected = true, IsDhcp = false,
        CurrentIp = "192.168.0.100", CurrentSubnet = "255.255.255.0",
        CurrentGateway = "192.168.0.1", CurrentPrimaryDns = dns
    };

    private static IpProfile Plant(string gateway = "192.168.0.1", string mask = "255.255.255.0", string dns = "") => new()
    {
        Name = "Plant", IpAddress = "192.168.0.100", SubnetMask = mask,
        Gateway = gateway, PrimaryDns = dns
    };

    private static bool IsActiveOn(IpProfile profile, AdapterInfo adapter)
    {
        var vm = new ProfileViewModel(profile);
        vm.UpdateFromAdapter(adapter);
        return vm.IsActive;
    }

    [Fact]
    public void Active_WhenAddressMaskAndGatewayMatch()
        => Assert.True(IsActiveOn(Plant(), StaticAdapter()));

    [Fact]
    public void NotActive_WhenOnlyTheIpMatches_GatewayDiffers()
        => Assert.False(IsActiveOn(Plant(gateway: "192.168.0.254"), StaticAdapter()));

    [Fact]
    public void NotActive_WhenMaskDiffers()
        => Assert.False(IsActiveOn(Plant(mask: "255.255.0.0"), StaticAdapter()));

    [Fact]
    public void NotActive_WhenProfileHasNoGatewayButAdapterDoes()
        => Assert.False(IsActiveOn(Plant(gateway: ""), StaticAdapter()));

    [Fact]
    public void NotActive_WhenDnsIsKnownOnBothSidesAndDiffers()
        => Assert.False(IsActiveOn(Plant(dns: "8.8.8.8"), StaticAdapter(dns: "1.1.1.1")));

    [Fact]
    public void Active_WhenAdapterReportsNoDns()   // disconnected adapters read no DNS
        => Assert.True(IsActiveOn(Plant(dns: "8.8.8.8"), StaticAdapter(dns: "")));

    [Fact]
    public void StaticProfile_NotActive_OnDhcpAdapterWithTheSameAddress()
    {
        var adapter = StaticAdapter();
        adapter.IsDhcp = true;
        Assert.False(IsActiveOn(Plant(), adapter));
    }

    [Fact]
    public void DhcpProfile_ActiveOnlyOnDhcpAdapter()
    {
        var dhcp = new IpProfile { Name = "Office", IsDhcp = true };
        var adapter = StaticAdapter();
        Assert.False(IsActiveOn(dhcp, adapter));
        adapter.IsDhcp = true;
        Assert.True(IsActiveOn(dhcp, adapter));
    }

    [Fact]
    public void NotActive_BeforeAnyAdapterState()
        => Assert.False(new ProfileViewModel(Plant()).IsActive);

    // ── Ctrl+N labels ────────────────────────────────────────────

    private static AdapterViewModel AdapterWith(int profileCount, out ObservableCollection<ProfileViewModel> profiles)
    {
        profiles = new ObservableCollection<ProfileViewModel>();
        for (int i = 0; i < profileCount; i++)
            profiles.Add(new ProfileViewModel(new IpProfile { Name = $"P{i}", IpAddress = $"10.0.0.{i + 1}" }));
        return new AdapterViewModel(StaticAdapter(), profiles, new ObservableCollection<HistoryEntry>());
    }

    [Fact]
    public void ShortcutLabels_FollowListPosition()
    {
        AdapterWith(3, out var profiles);
        Assert.Equal(new[] { "Ctrl+1", "Ctrl+2", "Ctrl+3" }, profiles.Select(p => p.ShortcutLabel));
    }

    [Fact]
    public void ShortcutLabels_EmptyPastTheLastHotkey()
    {
        // A hand-edited or older profiles file can hold more than the UI allows.
        AdapterWith(MainViewModel.MaxProfilesPerAdapter + 1, out var profiles);
        Assert.Equal("Ctrl+5", profiles[MainViewModel.MaxProfilesPerAdapter - 1].ShortcutLabel);
        Assert.Equal("", profiles[MainViewModel.MaxProfilesPerAdapter].ShortcutLabel);
    }

    [Fact]
    public void ShortcutLabels_RenumberAfterDelete()
    {
        AdapterWith(3, out var profiles);
        profiles.RemoveAt(0);
        Assert.Equal(new[] { "Ctrl+1", "Ctrl+2" }, profiles.Select(p => p.ShortcutLabel));
    }

    [Fact]
    public void AddedProfile_GetsSlotAndActiveStateImmediately()
    {
        AdapterWith(1, out var profiles);
        var added = new ProfileViewModel(Plant());
        profiles.Add(added);
        Assert.Equal("Ctrl+2", added.ShortcutLabel);
        Assert.True(added.IsActive);
    }
}
