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
        private string _selectedTab = "Online";

        public OnlineOrdersViewModel()
        {
            OnlineOrders = new ObservableCollection<OnlineOrder>();
            RegisterOrders = new ObservableCollection<OnlineOrder>();

            StatusOptions = new ObservableCollection<string>
            {
                "pending",
                "confirmed",
                "adjustment",
                "preparing",
                "completed",
                "cancelled"
            };

            OnlineOrdersView = CollectionViewSource.GetDefaultView(OnlineOrders);
            RegisterOrdersView = CollectionViewSource.GetDefaultView(RegisterOrders);
            OnlineOrdersView.Filter = FilterOrders;
            RegisterOrdersView.Filter = FilterOrders;

            RefreshCommand = new RelayCommand(_ => LoadOrders());
            ShowOnlineOrdersCommand = new RelayCommand(_ => SelectTab("Online"));
            ShowRegisterOrdersCommand = new RelayCommand(_ => SelectTab("Register"));
            ConfirmOrderCommand = new RelayCommand(_ => ApplyQuickStatus("confirmed"), _ => CanEditOnlineOrder);
            NeedsAdjustmentCommand = new RelayCommand(_ => ApplyQuickStatus("adjustment"), _ => CanEditOnlineOrder);
            MarkCompleteCommand = new RelayCommand(_ => ApplyQuickStatus("completed"), _ => CanEditOnlineOrder);
            CancelOrderCommand = new RelayCommand(_ => ApplyQuickStatus("cancelled"), _ => CanEditOnlineOrder);
            SaveStatusCommand = new RelayCommand(_ => SaveSelectedStatus(), _ => CanEditOnlineOrder);

            LoadOrders();
        }

        public ObservableCollection<OnlineOrder> OnlineOrders { get; }
        public ObservableCollection<OnlineOrder> RegisterOrders { get; }
        public ObservableCollection<string> StatusOptions { get; }
        public ICollectionView OnlineOrdersView { get; }
        public ICollectionView RegisterOrdersView { get; }

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
                OnlineOrdersView.Refresh();
                RegisterOrdersView.Refresh();
                SyncSelection();
                RaiseSummaryProperties();
            }
        }

        public string SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (_selectedTab == value)
                {
                    return;
                }

                _selectedTab = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsOnlineTabSelected));
                OnPropertyChanged(nameof(IsRegisterTabSelected));
                OnPropertyChanged(nameof(ActiveOrdersView));
                OnPropertyChanged(nameof(ActiveTabLabel));
                OnPropertyChanged(nameof(PrimaryMetricLabel));
                OnPropertyChanged(nameof(SecondaryMetricLabel));
                OnPropertyChanged(nameof(CanEditOnlineOrder));
                SyncSelection();
                RaiseSummaryProperties();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsOnlineTabSelected => string.Equals(SelectedTab, "Online", System.StringComparison.Ordinal);
        public bool IsRegisterTabSelected => string.Equals(SelectedTab, "Register", System.StringComparison.Ordinal);
        public string ActiveTabLabel => IsOnlineTabSelected ? "Online Orders" : "Register Orders";
        public string PrimaryMetricLabel => IsOnlineTabSelected ? "Orders" : "Walk-ins";
        public string SecondaryMetricLabel => IsOnlineTabSelected ? "Awaiting Action" : "Paid Tickets";

        public ICollectionView ActiveOrdersView => IsOnlineTabSelected ? OnlineOrdersView : RegisterOrdersView;

        public OnlineOrder? SelectedOrder
        {
            get => _selectedOrder;
            set
            {
                _selectedOrder = value;
                SelectedStatus = value?.Status ?? StatusOptions.First();
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedOrder));
                OnPropertyChanged(nameof(CanEditOnlineOrder));
                OnPropertyChanged(nameof(SelectedOrderItemCount));
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
        public bool CanEditOnlineOrder => IsOnlineTabSelected && SelectedOrder != null;
        public int ActiveOrderCount => ActiveOrdersView.Cast<object>().Count();
        public decimal ActiveRevenue => ActiveOrdersView.Cast<OnlineOrder>().Sum(order => order.Total);
        public decimal ActiveAverageTicket => ActiveOrderCount == 0 ? 0 : ActiveRevenue / ActiveOrderCount;
        public int SecondaryMetricValue => IsOnlineTabSelected
            ? ActiveOrdersView.Cast<OnlineOrder>().Count(order => order.Status != "completed" && order.Status != "cancelled")
            : ActiveOrdersView.Cast<OnlineOrder>().Count(order => order.PaymentStatus == "paid");
        public int SelectedOrderItemCount => SelectedOrder?.Items.Sum(item => item.Quantity) ?? 0;

        public string TopProductName
        {
            get
            {
                var product = ActiveOrdersView
                    .Cast<OnlineOrder>()
                    .SelectMany(order => order.Items)
                    .GroupBy(item => item.Name)
                    .OrderByDescending(group => group.Sum(item => item.Quantity))
                    .FirstOrDefault();

                return product?.Key ?? "No orders yet";
            }
        }

        public int TopProductSales
        {
            get
            {
                return ActiveOrdersView
                    .Cast<OnlineOrder>()
                    .SelectMany(order => order.Items)
                    .GroupBy(item => item.Name)
                    .OrderByDescending(group => group.Sum(item => item.Quantity))
                    .Select(group => group.Sum(item => item.Quantity))
                    .FirstOrDefault();
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand ShowOnlineOrdersCommand { get; }
        public ICommand ShowRegisterOrdersCommand { get; }
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

            return order.Id.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ||
                order.DisplayName.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ||
                order.CustomerPhone.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ||
                order.CustomerEmail.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ||
                order.Status.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ||
                order.SourceLabel.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase);
        }

        private void LoadOrders()
        {
            try
            {
                OnlineOrders.Clear();
                RegisterOrders.Clear();

                foreach (var order in _orderRepository.GetOnlineOrders())
                {
                    OnlineOrders.Add(order);
                }

                foreach (var order in _orderRepository.GetRegisterOrders())
                {
                    RegisterOrders.Add(order);
                }

                OnlineOrdersView.Refresh();
                RegisterOrdersView.Refresh();
                SyncSelection();
                RaiseSummaryProperties();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"Unable to load order history.\n\n{ex.Message}",
                    "Orders Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SelectTab(string tab)
        {
            SelectedTab = tab;
        }

        private void SyncSelection()
        {
            var activeOrders = ActiveOrdersView.Cast<OnlineOrder>().ToList();

            if (SelectedOrder != null && activeOrders.Contains(SelectedOrder))
            {
                return;
            }

            SelectedOrder = activeOrders.FirstOrDefault();
        }

        private void ApplyQuickStatus(string status)
        {
            SelectedStatus = status;
            SaveSelectedStatus();
        }

        private void SaveSelectedStatus()
        {
            if (!CanEditOnlineOrder || SelectedOrder == null)
            {
                return;
            }

            try
            {
                _orderRepository.UpdateOrderStatus(SelectedOrder.Id, SelectedStatus);
                SelectedOrder.Status = SelectedStatus;
                OnPropertyChanged(nameof(SelectedOrder));
                RaiseSummaryProperties();
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

        private void RaiseSummaryProperties()
        {
            OnPropertyChanged(nameof(ActiveOrderCount));
            OnPropertyChanged(nameof(ActiveRevenue));
            OnPropertyChanged(nameof(ActiveAverageTicket));
            OnPropertyChanged(nameof(SecondaryMetricValue));
            OnPropertyChanged(nameof(TopProductName));
            OnPropertyChanged(nameof(TopProductSales));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
