using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ddph.ViewModels;

namespace ddph
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            var viewModel = new MainWindowViewModel();
            viewModel.PaymentFocusRequested += FocusPaymentTextBox;
            DataContext = viewModel;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var productsWindow = new Products
            {
                Owner = this
            };

            productsWindow.Show();
        }

        private void OrdersButton_Click(object sender, RoutedEventArgs e)
        {
            var ordersWindow = new OnlineOrders
            {
                Owner = this
            };

            ordersWindow.Show();
        }

        private void CustomItemsButton_Click(object sender, RoutedEventArgs e)
        {
            var customItemsWindow = new CustomItemsWindow
            {
                Owner = this
            };

            customItemsWindow.Show();
        }

        private void FocusPaymentTextBox()
        {
            PaymentTextBox.Focus();
            PaymentTextBox.SelectAll();
        }
    }

}
