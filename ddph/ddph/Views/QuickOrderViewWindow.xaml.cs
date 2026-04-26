using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ddph.Data;
using ddph.Models;

namespace ddph.Views
{
    public partial class QuickOrderViewWindow : Window
    {
        private readonly OrderRepository _orderRepository = new();
        private readonly string[] _statusOptions =
        [
            "pending",
            "confirmed",
            "adjustment",
            "preparing",
            "completed",
            "cancelled"
        ];
        private OnlineOrder? _currentOrder;

        public QuickOrderViewWindow()
        {
            InitializeComponent();
            StatusComboBox.ItemsSource = _statusOptions;
            Loaded += (_, _) =>
            {
                ReferenceTextBox.Focus();
                ReferenceTextBox.SelectAll();
            };
        }

        private async void ViewButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadOrderAsync();
        }

        private async void ReferenceTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            e.Handled = true;
            await LoadOrderAsync();
        }

        private void ReferenceTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(ReferenceTextBox.Text))
            {
                return;
            }

            ShowMessage("Type reference");
        }

        private async Task LoadOrderAsync()
        {
            var reference = NormalizeReference(ReferenceTextBox.Text);
            if (string.IsNullOrWhiteSpace(reference))
            {
                ShowMessage("Type reference");
                return;
            }

            SetLoading(true);

            try
            {
                var order = await _orderRepository.GetOrderByReferenceAsync(reference);
                if (order == null)
                {
                    ShowMessage("Order not found");
                    return;
                }

                ShowOrder(order);
            }
            catch (Exception ex)
            {
                ShowMessage($"Unable to load order. {ex.Message}");
            }
            finally
            {
                SetLoading(false);
            }
        }

        private void ShowOrder(OnlineOrder order)
        {
            _currentOrder = order;
            EmptyTextBlock.Visibility = Visibility.Collapsed;
            OrderScrollViewer.Visibility = Visibility.Visible;

            OrderIdTextBlock.Text = order.Id;
            OrderMetaTextBlock.Text = $"{order.SourceLabel} | {order.Date}";
            StatusTextBlock.Text = order.Status;
            StatusComboBox.SelectedItem = _statusOptions.Contains(order.Status)
                ? order.Status
                : _statusOptions[0];
            CustomerTextBlock.Text = order.DisplayName;
            ContactTextBlock.Text = BuildContactText(order);
            NotesTextBlock.Text = string.IsNullOrWhiteSpace(order.Notes) ? "No notes" : $"Notes: {order.Notes}";
            ItemsControl.ItemsSource = order.Items;
            SubtotalTextBlock.Text = $"Subtotal: {FormatCurrency(order.Subtotal)}";
            PaymentTextBlock.Text = order.Payment > 0 ? $"Payment: {FormatCurrency(order.Payment)}" : string.Empty;
            ChangeTextBlock.Text = order.Change > 0 ? $"Change: {FormatCurrency(order.Change)}" : string.Empty;
            TotalTextBlock.Text = $"Total: {FormatCurrency(order.Total)}";
        }

        private static string BuildContactText(OnlineOrder order)
        {
            var parts = new[] { order.CustomerPhone, order.CustomerEmail }
                .Where(part => !string.IsNullOrWhiteSpace(part));

            var contact = string.Join(" | ", parts);
            return string.IsNullOrWhiteSpace(contact) ? "No contact info" : contact;
        }

        private void ShowMessage(string message)
        {
            _currentOrder = null;
            OrderScrollViewer.Visibility = Visibility.Collapsed;
            EmptyTextBlock.Visibility = Visibility.Visible;
            EmptyTextBlock.Text = message;
        }

        private void SetLoading(bool isLoading)
        {
            ViewButton.IsEnabled = !isLoading;
            ReferenceTextBox.IsEnabled = !isLoading;
            SaveStatusButton.IsEnabled = !isLoading && _currentOrder != null;
            if (isLoading)
            {
                ShowMessage("Loading order");
            }
        }

        private async void SaveStatusButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentOrder == null || StatusComboBox.SelectedItem is not string status)
            {
                return;
            }

            ViewButton.IsEnabled = false;
            ReferenceTextBox.IsEnabled = false;
            SaveStatusButton.IsEnabled = false;

            try
            {
                await _orderRepository.UpdateOrderStatusAsync(_currentOrder.Id, status, GetOrderNode(_currentOrder));
                _currentOrder.Status = status;
                StatusTextBlock.Text = status;
                OrderScrollViewer.Visibility = Visibility.Visible;
                EmptyTextBlock.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                ShowMessage($"Unable to save status. {ex.Message}");
            }
            finally
            {
                SetLoading(false);
            }
        }

        private static string GetOrderNode(OnlineOrder order)
        {
            return order.SourceLabel switch
            {
                "Register" => "walk-in-orders",
                "Kiosk" => "kioskSales",
                "Custom" => "customOrders",
                _ => "orders"
            };
        }

        private static string NormalizeReference(string reference)
        {
            var trimmed = reference.Trim();
            var markerIndex = trimmed.IndexOf("Reference:", StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0)
            {
                trimmed = trimmed[(markerIndex + "Reference:".Length)..].Trim();
            }

            return trimmed.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? string.Empty;
        }

        private static string FormatCurrency(decimal value)
        {
            return value.ToString("C", CultureInfo.CurrentCulture);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
