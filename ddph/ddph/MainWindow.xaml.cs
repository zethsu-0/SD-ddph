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
using ddph.Views;

namespace ddph
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private UIElement? _inventoryContent;
        private UIElement? _ordersContent;
        private UIElement? _customContent;

        public MainWindow()
        {
            InitializeComponent();
            var viewModel = new MainWindowViewModel();
            viewModel.PaymentFocusRequested += FocusPaymentTextBox;
            DataContext = viewModel;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ShowInventoryTab();
        }

        private void OrdersButton_Click(object sender, RoutedEventArgs e)
        {
            ShowOrdersTab();
        }

        private void CustomItemsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowCustomTab();
        }

        private void RegisterTab_Click(object sender, RoutedEventArgs e)
        {
            ShowRegisterTab();
        }

        private void FocusPaymentTextBox()
        {
            PaymentTextBox.Focus();
            PaymentTextBox.SelectAll();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ShowRegisterTab()
        {
            RegisterContent.Visibility = Visibility.Visible;
            TabContentHost.Visibility = Visibility.Collapsed;
            TabContentHost.Content = null;
            MainSearchPanel.Visibility = Visibility.Visible;
        }

        private void ShowInventoryTab()
        {
            _inventoryContent ??= new InventoryView();

            ShowEmbeddedTab(_inventoryContent);
        }

        private void ShowOrdersTab()
        {
            _ordersContent ??= CreateEmbeddedWindowContent(new OnlineOrders());
            ShowEmbeddedTab(_ordersContent);
        }

        private void ShowCustomTab()
        {
            _customContent ??= CreateEmbeddedWindowContent(new CustomItemsWindow());
            ShowEmbeddedTab(_customContent);
        }

        private void ShowEmbeddedTab(UIElement? content)
        {
            if (content == null)
            {
                return;
            }

            RegisterContent.Visibility = Visibility.Collapsed;
            TabContentHost.Content = content;
            TabContentHost.Visibility = Visibility.Visible;
            MainSearchPanel.Visibility = Visibility.Collapsed;
        }

        private static UIElement? CreateEmbeddedWindowContent(Window sourceWindow)
        {
            if (sourceWindow.Content is not UIElement content)
            {
                return null;
            }

            sourceWindow.Content = null;

            if (content is FrameworkElement element)
            {
                element.DataContext = sourceWindow.DataContext;
            }

            if (content is Grid rootGrid && rootGrid.Children.Count > 0 && rootGrid.Children[0] is Button overlayButton)
            {
                overlayButton.Visibility = Visibility.Collapsed;
            }

            return content;
        }
    }

}
