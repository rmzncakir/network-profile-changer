using System.Windows;
using System.Windows.Controls;
using NetworkProfileManager.ViewModels;

namespace NetworkProfileManager.Views
{
    public partial class ProfilePanel : UserControl
    {
        public ProfilePanel() => InitializeComponent();

        private void AddProfile_Click(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is MainViewModel vm)) return;
            if (vm.SelectedAdapter == null) return;

            var dialog = new AddProfileDialog
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true && dialog.Result != null)
                vm.AddProfile(dialog.Result);
        }

        private void EditProfile_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement fe && fe.Tag is ProfileViewModel pvm)) return;
            if (!(DataContext is MainViewModel vm) || vm.SelectedAdapter == null) return;

            var dialog = new AddProfileDialog(pvm.Model)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true && dialog.Result != null)
                vm.UpdateProfile(pvm, dialog.Result);
        }
    }
}
