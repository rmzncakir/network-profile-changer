using System.Collections.ObjectModel;
using NetworkProfileManager.Models;
using NetworkProfileManager.ViewModels;

namespace NetworkProfileManager.Tests;

/// <summary>
/// AdapterViewModel uses WPF's IDataErrorInfo — TextBox binding's
/// ValidatesOnDataErrors=True drives the red border.
/// </summary>
public class AdapterViewModelValidationTests
{
    private static AdapterViewModel CreateVm(AdapterInfo? info = null)
    {
        info ??= new AdapterInfo
        {
            Id = "1", Name = "Ethernet", IsConnected = true,
            CurrentIp = "192.168.1.10", CurrentSubnet = "255.255.255.0",
            CurrentGateway = "192.168.1.1", IsDhcp = false
        };
        return new AdapterViewModel(info,
            new ObservableCollection<ProfileViewModel>(),
            new ObservableCollection<HistoryEntry>());
    }

    [Fact]
    public void Initial_StaticConfiguration_IsValid()
    {
        var vm = CreateVm();
        Assert.True(vm.IsEditValid);
        Assert.Equal("", vm["EditIp"]);
        Assert.Equal("", vm["EditSubnet"]);
        Assert.Equal("", vm["EditGateway"]);
    }

    [Fact]
    public void InvalidIp_ReportsErrorViaIDataErrorInfo()
    {
        var vm = CreateVm();
        vm.EditIp = "not-an-ip";

        Assert.False(vm.IsEditValid);
        Assert.Contains("IP", vm["EditIp"]);
    }

    [Fact]
    public void IPAddressShorthand_IsRejected()
    {
        // .NET's IPAddress.TryParse accepts "192.168.1" as 192.168.0.1.
        // Our canonical-dotted-quad check rejects it.
        var vm = CreateVm();
        vm.EditIp = "192.168.1";

        Assert.False(vm.IsEditValid);
        Assert.Contains("IP", vm["EditIp"]);
    }

    [Fact]
    public void ValidIp_ClearsError()
    {
        var vm = CreateVm();
        vm.EditIp = "garbage";
        Assert.False(vm.IsEditValid);

        vm.EditIp = "10.0.0.5";

        Assert.True(vm.IsEditValid);
        Assert.Equal("", vm["EditIp"]);
    }

    [Fact]
    public void EmptyGateway_IsAccepted()
    {
        var vm = CreateVm();
        vm.EditGateway = "";
        Assert.True(vm.IsEditValid);
        Assert.Equal("", vm["EditGateway"]);
    }

    [Fact]
    public void InvalidGateway_IsRejected()
    {
        var vm = CreateVm();
        vm.EditGateway = "999.0.0.0";
        Assert.False(vm.IsEditValid);
        Assert.NotEqual("", vm["EditGateway"]);
    }

    [Fact]
    public void DhcpMode_SuppressesValidationErrors()
    {
        var vm = CreateVm();
        vm.EditIp     = "garbage";
        vm.EditSubnet = "also-bad";
        Assert.False(vm.IsEditValid);

        vm.EditIsDhcp = true;

        Assert.True(vm.IsEditValid);
        Assert.Equal("", vm["EditIp"]);
        Assert.Equal("", vm["EditSubnet"]);
    }

    [Fact]
    public void IsCanonicalIp_AcceptsValidDottedQuad()
    {
        Assert.True(AdapterViewModel.IsCanonicalIp("192.168.1.1"));
        Assert.True(AdapterViewModel.IsCanonicalIp("0.0.0.0"));
        Assert.True(AdapterViewModel.IsCanonicalIp("255.255.255.255"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("192.168.1")]
    [InlineData("192.168.1.")]
    [InlineData(".192.168.1.1")]
    [InlineData("192.168.1.1.1")]
    [InlineData("256.0.0.1")]
    [InlineData("not-an-ip")]
    public void IsCanonicalIp_RejectsInvalid(string ip)
    {
        Assert.False(AdapterViewModel.IsCanonicalIp(ip));
    }
}
