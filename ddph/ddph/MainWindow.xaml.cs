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
using System.Linq;
using System.IO;
using ddph.ViewModels;
using ddph.Views;
using ddph.Receipts;

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
        private readonly Brush _activeNavBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
        private readonly Brush _inactiveNavBrush = Brushes.Transparent;

        public MainWindow()
        {
            InitializeComponent();
            var viewModel = new MainWindowViewModel();
            viewModel.PaymentFocusRequested += FocusPaymentTextBox;
            viewModel.ReceiptGenerated += ShowReceiptPreview;
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

        private void ShowReceiptPreview(ReceiptPdfResult receipt)
        {
            var previewWindow = new ReceiptPreviewWindow(receipt.FilePath, receipt.PreviewImages)
            {
                Owner = this
            };
            previewWindow.ShowDialog();
        }

        private void PaymentTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                return;
            }

            var nextText = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength);
            nextText = nextText.Insert(textBox.CaretIndex, e.Text);
            e.Handled = !decimal.TryParse(nextText, out _);
        }

        private void CartQtyTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = e.Text.Any(character => !char.IsDigit(character));
        }

        private void CartQtyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitCartQuantityEdit(sender as TextBox);
        }

        private void CartQtyTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            CommitCartQuantityEdit(sender as TextBox);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void DiscountButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var discountWindow = new DiscountWindow(viewModel.DiscountRate, viewModel.HasDiscount ? viewModel.DiscountTypeLabel : null)
            {
                Owner = this
            };

            var result = discountWindow.ShowDialog();
            if (result == true)
            {
                viewModel.ApplyDiscount(discountWindow.SelectedDiscountRate, discountWindow.SelectedCustomerType);
                return;
            }

            if (discountWindow.WasCleared)
            {
                viewModel.ClearDiscount();
            }
        }

        private void ShowRegisterTab()
        {
            PageTitleText.Text = "Register";
            SetActiveNavButton(RegisterNavButton);
            RegisterContent.Visibility = Visibility.Visible;
            TabContentHost.Visibility = Visibility.Collapsed;
            TabContentHost.Content = null;
            MainSearchPanel.Visibility = Visibility.Visible;

            if (DataContext is MainWindowViewModel viewModel && viewModel.RefreshProductsCommand.CanExecute(null))
            {
                viewModel.RefreshProductsCommand.Execute(null);
            }
        }

        private void ShowInventoryTab()
        {
            _inventoryContent ??= new InventoryView();
            PageTitleText.Text = "Inventory";
            SetActiveNavButton(InventoryNavButton);
            ShowEmbeddedTab(_inventoryContent);
        }

        private void ShowOrdersTab()
        {
            _ordersContent ??= CreateEmbeddedWindowContent(new OnlineOrders());
            PageTitleText.Text = "Orders";
            SetActiveNavButton(OrdersNavButton);
            ShowEmbeddedTab(_ordersContent);
        }

        private void ShowCustomTab()
        {
            _customContent ??= CreateEmbeddedWindowContent(new CustomItemsWindow());
            PageTitleText.Text = "Custom";
            SetActiveNavButton(CustomNavButton);
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

        private void CommitCartQuantityEdit(TextBox? textBox)
        {
            if (textBox?.DataContext is not CartItem cartItem ||
                DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            if (!int.TryParse(textBox.Text, out var quantity))
            {
                textBox.Text = cartItem.Qty.ToString();
                return;
            }

            if (quantity == 0)
            {
                viewModel.RemoveCartItem(cartItem);
                return;
            }

            if (quantity > 100)
            {
                quantity = 100;
            }

            viewModel.UpdateCartItemQuantity(cartItem, quantity);
            textBox.Text = quantity.ToString();
        }

        private void SetActiveNavButton(Button activeButton)
        {
            RegisterNavButton.Background = _inactiveNavBrush;
            OrdersNavButton.Background = _inactiveNavBrush;
            InventoryNavButton.Background = _inactiveNavBrush;
            CustomNavButton.Background = _inactiveNavBrush;
            activeButton.Background = _activeNavBrush;
        }

    }

}
