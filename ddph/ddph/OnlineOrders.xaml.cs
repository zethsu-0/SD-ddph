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
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
