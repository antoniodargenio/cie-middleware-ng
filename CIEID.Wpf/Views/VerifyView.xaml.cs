using System.Windows;
using System.Windows.Controls;
using CIEID.Wpf.ViewModels;

namespace CIEID.Wpf.Views
{
    public partial class VerifyView : UserControl
    {
        public VerifyView() { InitializeComponent(); }

        private void OnDrop(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            VerifyViewModel vm = DataContext as VerifyViewModel;
            if (files != null && files.Length > 0 && vm != null)
            {
                vm.FilePath = files[0];
                vm.VerifyCommand.Execute(null);
            }
        }
    }
}
