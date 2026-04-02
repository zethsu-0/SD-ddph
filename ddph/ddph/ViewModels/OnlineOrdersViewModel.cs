using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ddph.Data;
using ddph.Models;

namespace ddph.ViewModels
{
    public class OnlineOrdersViewModel : INotifyPropertyChanged
    {
        private readonly OrderRepository _orderRepository = new();
        private string _searchText = string.Empty;
        private OnlineOrder? _selectedOrder;
        private string _selectedStatus = "pending";

        public OnlineOrdersViewModel()
        {
            Orders = new ObservableCollection<OnlineOrder>();
            StatusOptions = new ObservableCollection<string>
            {
                "pending",
                "confirmed",
                "adjustment",
                "preparing",
                "completed",
                "cancelled"
            };

            FilteredOrders = CollectionViewSource.GetDefaultView(Orders);
            FilteredOrders.Filter = FilterOrders;

            RefreshCommand = new RelayCommand(_ => LoadOrders());
            ConfirmOrderCommand = new RelayCommand(_ => ApplyQuickStatus("confirmed"), _ => SelectedOrder != null);
            NeedsAdjustmentCommand = new RelayCommand(_ => ApplyQuickStatus("adjustment"), _ => SelectedOrder != null);
            MarkCompleteCommand = new RelayCommand(_ => ApplyQuickStatus("completed"), _ => SelectedOrder != null);
            CancelOrderCommand = new RelayCommand(_ => ApplyQuickStatus("cancelled"), _ => SelectedOrder != null);
            SaveStatusCommand = new RelayCommand(_ => SaveSelectedStatus(), _ => SelectedOrder != null);

            LoadOrders();
        }

        public ObservableCollection<OnlineOrder> Orders { get; }
        public ObservableCollection<string> StatusOptions { get; }
        public ICollectionView FilteredOrders { get; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value)
                {
                    return;
                }

                _searchText = value;
                OnPropertyChanged();
                FilteredOrders.Refresh();
            }
        }

        public OnlineOrder? SelectedOrder
        {
            get => _selectedOrder;
            set
            {
                _selectedOrder = value;
                SelectedStatus = value?.Status ?? StatusOptions.First();
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedOrder));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (_selectedStatus == value)
                {
                    return;
                }

                _selectedStatus = value;
                OnPropertyChanged();
            }
        }

        public bool HasSelectedOrder => SelectedOrder != null;

        public ICommand RefreshCommand { get; }
        public ICommand ConfirmOrderCommand { get; }
        public ICommand NeedsAdjustmentCommand { get; }
        public ICommand MarkCompleteCommand { get; }
        public ICommand CancelOrderCommand { get; }
        public ICommand SaveStatusCommand { get; }

        private bool FilterOrders(object item)
        {
            if (item is not OnlineOrder order)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return true;
            }

            return order.DisplayName.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ||
                order.CustomerPhone.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ||
                order.CustomerEmail.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ||
                order.Status.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase);
        }

        private void LoadOrders()
        {
            try
            {
                Orders.Clear();

                foreach (var order in _orderRepository.GetOrders())
                {
                    Orders.Add(order);
                }

                FilteredOrders.Refresh();
                SelectedOrder ??= Orders.FirstOrDefault();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"Unable to load online orders.\n\n{ex.Message}",
                    "Orders Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ApplyQuickStatus(string status)
        {
            SelectedStatus = status;
            SaveSelectedStatus();
        }

        private void SaveSelectedStatus()
        {
            if (SelectedOrder == null)
            {
                return;
            }

            try
            {
                _orderRepository.UpdateOrderStatus(SelectedOrder.Id, SelectedStatus);
                SelectedOrder.Status = SelectedStatus;
                OnPropertyChanged(nameof(SelectedOrder));
                MessageBox.Show("Order status updated.", "Orders", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"Unable to update order status.\n\n{ex.Message}",
                    "Orders Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
