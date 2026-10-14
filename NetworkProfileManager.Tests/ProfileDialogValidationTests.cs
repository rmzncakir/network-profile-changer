using NetworkProfileManager.Views;

namespace NetworkProfileManager.Tests;

/// <summary>
/// The add/edit profile dialog should not open covered in red: empty required
/// fields are only flagged after a save attempt, while malformed input is
/// flagged as soon as it is typed.
/// </summary>
public class ProfileDialogValidationTests
{
    [Fact]
    public void NewDialog_DoesNotFlagEmptyRequiredFields()
    {
        var vm = new DialogViewModel();

        Assert.Equal("", vm[nameof(DialogViewModel.Name)]);
        Assert.Equal("", vm[nameof(DialogViewModel.IpAddress)]);
        Assert.False(vm.IsValid);
    }

    [Fact]
    public void AfterSaveAttempt_EmptyRequiredFieldsAreFlagged()
    {
        var vm = new DialogViewModel();

        vm.ShowRequiredErrors();

        Assert.NotEqual("", vm[nameof(DialogViewModel.Name)]);
        Assert.NotEqual("", vm[nameof(DialogViewModel.IpAddress)]);
    }

    [Fact]
    public void MalformedIp_IsFlaggedImmediately()
    {
        var vm = new DialogViewModel { IpAddress = "192.168.1" };

        Assert.NotEqual("", vm[nameof(DialogViewModel.IpAddress)]);
    }

    [Fact]
    public void Dhcp_NeverFlagsAddress()
    {
        var vm = new DialogViewModel { IsDhcp = true };

        vm.ShowRequiredErrors();

        Assert.Equal("", vm[nameof(DialogViewModel.IpAddress)]);
    }

    [Fact]
    public void ShowRequiredErrors_RaisesChangeForAllFields()
    {
        var vm = new DialogViewModel();
        string? raised = null;
        vm.PropertyChanged += (_, e) => raised = e.PropertyName;

        vm.ShowRequiredErrors();

        Assert.Equal(string.Empty, raised);
    }
}
