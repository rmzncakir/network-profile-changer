using System.Collections.Generic;
using System.Collections.ObjectModel;
using NetworkProfileManager.Models;
using NetworkProfileManager.ViewModels;

namespace NetworkProfileManager.Tests;

[Collection("StaticState")]
public class MainViewModelTests : System.IDisposable
{
    // MainViewModel loads profiles and history on construction; keep that away
    // from the real %LOCALAPPDATA% data.
    private readonly TestDataDirectoryFixture _dir = new();
    public void Dispose() => _dir.Dispose();

    private static AdapterViewModel Adapter(string id) => new(
        new AdapterInfo { Id = id, Name = id },
        new ObservableCollection<ProfileViewModel>(),
        new ObservableCollection<HistoryEntry>());

    /// <summary>
    /// Refresh() re-selects the same adapter after every apply, revert and
    /// refresh. The window resets its page on this notification, so repeating
    /// it for the same adapter made the page jump to the top after each action.
    /// </summary>
    [Fact]
    public void SelectingTheSameAdapterAgain_DoesNotNotify()
    {
        var vm = new MainViewModel();
        var a = Adapter("a");
        vm.SelectedAdapter = a;

        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.SelectedAdapter = a;
        Assert.DoesNotContain(nameof(MainViewModel.SelectedAdapter), raised);

        vm.SelectedAdapter = Adapter("b");
        Assert.Contains(nameof(MainViewModel.SelectedAdapter), raised);
    }

    [Fact]
    public void ErrorText_WithoutContext_IsThePlainMessage()
        // The notification title already says "Error"; no second "Error:" prefix.
        => Assert.Equal("Access is denied.", MainViewModel.ErrorText(null, "Access is denied."));
}
