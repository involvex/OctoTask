using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OctoTask.Core.Models;
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

        private void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (DataContext is PortViewModel vm)
            {
                var menu = (ContextMenu)((FrameworkElement)sender).ContextMenu;
                if (menu != null)
                {
                    menu.DataContext = vm.SelectedConnection;
                    e.Handled = vm.SelectedConnection == null;
                }
            }
        }

        private void OnCopyAddress(object sender, RoutedEventArgs e)
        {
            if (GetConnFromSender(sender) is ConnectionInfo conn && !string.IsNullOrEmpty(conn.LocalEndpoint))
                Clipboard.SetText(conn.LocalEndpoint);
        }

        private void OnCopyPort(object sender, RoutedEventArgs e)
        {
            if (GetConnFromSender(sender) is ConnectionInfo conn)
                Clipboard.SetText(conn.LocalPortDisplay);
        }

        private void OnCopyProtocol(object sender, RoutedEventArgs e)
        {
            if (GetConnFromSender(sender) is ConnectionInfo conn)
                Clipboard.SetText(conn.ProtocolDisplay);
        }

        private void OnCopyProcessName(object sender, RoutedEventArgs e)
        {
            if (GetConnFromSender(sender) is ConnectionInfo conn && !string.IsNullOrEmpty(conn.ProcessName))
                Clipboard.SetText(conn.ProcessName);
        }

        private static ConnectionInfo? GetConnFromSender(object sender)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ConnectionInfo conn)
                return conn;
            return null;
        }
    }
}
