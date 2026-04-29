using System.Windows;
using ddph.ViewModels;

namespace ddph
{
    public partial class OnlineOrders : Window
    {
        public OnlineOrders()
        {
            InitializeComponent();
            DataContext = new OnlineOrdersViewModel();
            Loaded += OnlineOrders_Loaded;
            Closed += OnlineOrders_Closed;
        }

        private async void OnlineOrders_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is OnlineOrdersViewModel viewModel)
            {
                await viewModel.RefreshWhenOpenedAsync();
            }
        }

        private void OnlineOrders_Closed(object? sender, System.EventArgs e)
        {
            if (DataContext is OnlineOrdersViewModel viewModel)
            {
                viewModel.StopAutoRefresh();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
