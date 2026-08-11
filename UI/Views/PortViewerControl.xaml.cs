using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OctoTask.UI.ViewModels;

namespace OctoTask.UI.Views
{
    public partial class PortViewerControl : UserControl
    {
        public PortViewerControl()
        {
            InitializeComponent();
        }

        private void OnDataGridDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is PortViewModel vm && vm.CanGoToProcess)
            {
                vm.GoToProcess();
            }
        }

        private void OnGoToProcessClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is PortViewModel vm && vm.CanGoToProcess)
            {
                vm.GoToProcess();
            }
        }
    }
}
