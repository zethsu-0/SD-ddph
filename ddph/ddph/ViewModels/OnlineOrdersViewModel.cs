using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
        private string _selectedTab = "Online";
        private string _sortProperty = nameof(OnlineOrder.Date);
        private ListSortDirection _sortDirection = ListSortDirection.Descending;
        private bool _isLoading;
        private int _activeOrderCount;
        private decimal _activeRevenue;
        private decimal _activeAverageTicket;
        private int _secondaryMetricValue;
        private string _topProductName = "No orders yet";
        private int _topProductSales;

        public OnlineOrdersViewModel()
        {
            OnlineOrders = new ObservableCollection<OnlineOrder>();
            CustomOrders = new ObservableCollection<OnlineOrder>();
            RegisterOrders = new ObservableCollection<OnlineOrder>();
            KioskSales = new ObservableCollection<OnlineOrder>();

            OnlineOrdersView = CollectionViewSource.GetDefaultView(OnlineOrders);
            CustomOrdersView = CollectionViewSource.GetDefaultView(CustomOrders);
            RegisterOrdersView = CollectionViewSource.GetDefaultView(RegisterOrders);
            KioskSalesView = CollectionViewSource.GetDefaultView(KioskSales);
            OnlineOrdersView.Filter = FilterOrders;
            CustomOrdersView.Filter = FilterOrders;
            RegisterOrdersView.Filter = FilterOrders;
            KioskSalesView.Filter = FilterOrders;

            RefreshCommand = new RelayCommand(async _ => await LoadActiveTabAsync(), _ => !IsLoading);
            ShowOnlineOrdersCommand = new RelayCommand(async _ => await SelectTabAsync("Online"), _ => !IsLoading);
            ShowCustomOrdersCommand = new RelayCommand(async _ => await SelectTabAsync("Custom"), _ => !IsLoading);
            ShowRegisterOrdersCommand = new RelayCommand(async _ => await SelectTabAsync("Register"), _ => !IsLoading);
            ShowKioskSalesCommand = new RelayCommand(async _ => await SelectTabAsync("Kiosk"), _ => !IsLoading);
            PendingOrderCommand = new RelayCommand(async _ => await ApplyQuickStatusAsync("pending"), _ => CanEditOnlineOrder);
            ConfirmOrderCommand = new RelayCommand(async _ => await ApplyQuickStatusAsync("confirmed"), _ => CanEditOnlineOrder);
            NeedsAdjustmentCommand = new RelayCommand(async _ => await ApplyQuickStatusAsync("adjustment"), _ => CanEditOnlineOrder);
            PreparingOrderCommand = new RelayCommand(async _ => await ApplyQuickStatusAsync("preparing"), _ => CanEditOnlineOrder);
            MarkCompleteCommand = new RelayCommand(async _ => await ApplyQuickStatusAsync("completed"), _ => CanEditOnlineOrder);
            CancelOrderCommand = new RelayCommand(async _ => await ApplyQuickStatusAsync("cancelled"), _ => CanEditOnlineOrder);
            SortOrdersCommand = new RelayCommand(parameter => SortOrders(parameter as string));

            ApplySort();
            _ = LoadActiveTabAsync();
        }

        public ObservableCollection<OnlineOrder> OnlineOrders { get; }
        public ObservableCollection<OnlineOrder> CustomOrders { get; }
        public ObservableCollection<OnlineOrder> RegisterOrders { get; }
        public ObservableCollection<OnlineOrder> KioskSales { get; }
        public ICollectionView OnlineOrdersView { get; }
        public ICollectionView CustomOrdersView { get; }
        public ICollectionView RegisterOrdersView { get; }
        public ICollectionView KioskSalesView { get; }

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
                ActiveOrdersView.Refresh();
                SyncSelection();
                UpdateSummaryProperties();
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
                OnPropertyChanged(nameof(IsCustomTabSelected));
                OnPropertyChanged(nameof(IsRegisterTabSelected));
                OnPropertyChanged(nameof(IsKioskTabSelected));
                OnPropertyChanged(nameof(ActiveOrdersView));
                OnPropertyChanged(nameof(ActiveTabLabel));
                OnPropertyChanged(nameof(PrimaryMetricLabel));
                OnPropertyChanged(nameof(SecondaryMetricLabel));
                OnPropertyChanged(nameof(CanEditOnlineOrder));
                ActiveOrdersView.Refresh();
                SyncSelection();
                UpdateSummaryProperties();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsOnlineTabSelected => string.Equals(SelectedTab, "Online", System.StringComparison.Ordinal);
        public bool IsCustomTabSelected => string.Equals(SelectedTab, "Custom", System.StringComparison.Ordinal);
        public bool IsRegisterTabSelected => string.Equals(SelectedTab, "Register", System.StringComparison.Ordinal);
        public bool IsKioskTabSelected => string.Equals(SelectedTab, "Kiosk", System.StringComparison.Ordinal);
        public string ActiveTabLabel => IsOnlineTabSelected ? "Online Orders" : IsCustomTabSelected ? "Custom Orders" : IsRegisterTabSelected ? "Register Orders" : "Kiosk Sales";
        public string PrimaryMetricLabel => IsRegisterTabSelected ? "Walk-ins" : IsKioskTabSelected ? "Kiosk Sales" : "Orders";
        public string SecondaryMetricLabel => IsRegisterTabSelected ? "Paid Tickets" : IsKioskTabSelected ? "Unpaid Tickets" : IsCustomTabSelected ? "Custom Queue" : "Awaiting Action";

        public ICollectionView ActiveOrdersView => IsOnlineTabSelected ? OnlineOrdersView : IsCustomTabSelected ? CustomOrdersView : IsRegisterTabSelected ? RegisterOrdersView : KioskSalesView;

        public OnlineOrder? SelectedOrder
        {
            get => _selectedOrder;
            set
            {
                _selectedOrder = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedOrder));
                OnPropertyChanged(nameof(CanEditOnlineOrder));
                OnPropertyChanged(nameof(SelectedOrderItemCount));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (_isLoading == value)
                {
                    return;
                }

                _isLoading = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanEditOnlineOrder));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool HasSelectedOrder => SelectedOrder != null;
        public bool CanEditOnlineOrder => SelectedOrder != null && !IsLoading;
        public int ActiveOrderCount => _activeOrderCount;
        public decimal ActiveRevenue => _activeRevenue;
        public decimal ActiveAverageTicket => _activeAverageTicket;
        public int SecondaryMetricValue => _secondaryMetricValue;
        public int SelectedOrderItemCount => SelectedOrder?.Items.Sum(item => item.Quantity) ?? 0;

        public string TopProductName => _topProductName;
        public int TopProductSales => _topProductSales;

        public ICommand RefreshCommand { get; }
        public ICommand ShowOnlineOrdersCommand { get; }
        public ICommand ShowCustomOrdersCommand { get; }
        public ICommand ShowRegisterOrdersCommand { get; }
        public ICommand ShowKioskSalesCommand { get; }
        public ICommand PendingOrderCommand { get; }
        public ICommand ConfirmOrderCommand { get; }
        public ICommand NeedsAdjustmentCommand { get; }
        public ICommand PreparingOrderCommand { get; }
        public ICommand MarkCompleteCommand { get; }
        public ICommand CancelOrderCommand { get; }
        public ICommand SortOrdersCommand { get; }

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
                order.ReferenceLabel.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ||
                order.DisplayName.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ||
                order.CustomerPhone.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ||
                order.CustomerEmail.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ||
                order.Status.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ||
                order.SourceLabel.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase);
        }

        private async Task LoadActiveTabAsync()
        {
            if (IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;
                if (IsRegisterTabSelected)
                {
                    ReplaceOrders(RegisterOrders, await _orderRepository.GetRegisterOrdersAsync());
                }
                else if (IsKioskTabSelected)
                {
                    ReplaceOrders(KioskSales, await _orderRepository.GetKioskSalesAsync());
                }
                else
                {
                    ReplaceOnlineOrders(await _orderRepository.GetOnlineOrdersAsync());
                }

                ActiveOrdersView.Refresh();
                ApplySort();
                SyncSelection();
                UpdateSummaryProperties();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"Unable to load order history.\n\n{ex.Message}",
                    "Orders Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SelectTabAsync(string tab)
        {
            SelectedTab = tab;
            await LoadActiveTabAsync();
        }

        private static void ReplaceOrders(ObservableCollection<OnlineOrder> target, System.Collections.Generic.IEnumerable<OnlineOrder> orders)
        {
            target.Clear();

            foreach (var order in orders)
            {
                target.Add(order);
            }
        }

        private void ReplaceOnlineOrders(System.Collections.Generic.IEnumerable<OnlineOrder> orders)
        {
            OnlineOrders.Clear();
            CustomOrders.Clear();

            foreach (var order in orders)
            {
                if (string.Equals(order.OrderType, "custom", System.StringComparison.OrdinalIgnoreCase))
                {
                    CustomOrders.Add(order);
                }
                else
                {
                    OnlineOrders.Add(order);
                }
            }
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

        private void SortOrders(string? propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                return;
            }

            if (string.Equals(_sortProperty, propertyName, System.StringComparison.Ordinal))
            {
                _sortDirection = _sortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }
            else
            {
                _sortProperty = propertyName;
                _sortDirection = propertyName == nameof(OnlineOrder.Total) || propertyName == nameof(OnlineOrder.Date)
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }

            ApplySort();
            SyncSelection();
        }

        private void ApplySort()
        {
            foreach (var view in new[] { OnlineOrdersView, CustomOrdersView, RegisterOrdersView, KioskSalesView })
            {
                view.SortDescriptions.Clear();
                view.SortDescriptions.Add(new SortDescription(_sortProperty, _sortDirection));
            }
        }

        private async Task ApplyQuickStatusAsync(string status)
        {
            await SaveSelectedStatusAsync(status);
        }

        private async Task SaveSelectedStatusAsync(string selectedStatus)
        {
            if (!CanEditOnlineOrder || SelectedOrder == null)
            {
                return;
            }

            try
            {
                IsLoading = true;
                var selectedOrder = SelectedOrder;
                await _orderRepository.UpdateOrderStatusAsync(selectedOrder.Id, selectedStatus, ActiveOrderNode);
                selectedOrder.Status = selectedStatus;
                OnPropertyChanged(nameof(SelectedOrder));
                UpdateSummaryProperties();
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
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateSummaryProperties()
        {
            var activeOrders = ActiveOrdersView.Cast<OnlineOrder>().ToList();
            var topProduct = activeOrders
                .SelectMany(order => order.Items)
                .GroupBy(item => item.Name)
                .Select(group => new
                {
                    Name = group.Key,
                    Quantity = group.Sum(item => item.Quantity)
                })
                .OrderByDescending(group => group.Quantity)
                .FirstOrDefault();

            _activeOrderCount = activeOrders.Count;
            _activeRevenue = activeOrders.Sum(order => order.Total);
            _activeAverageTicket = _activeOrderCount == 0 ? 0 : _activeRevenue / _activeOrderCount;
            _secondaryMetricValue = IsRegisterTabSelected
                ? activeOrders.Count(order => order.PaymentStatus == "paid")
                : IsKioskTabSelected
                    ? activeOrders.Count(order => order.PaymentStatus != "paid")
                    : activeOrders.Count(order => order.Status != "completed" && order.Status != "cancelled");
            _topProductName = topProduct?.Name ?? "No orders yet";
            _topProductSales = topProduct?.Quantity ?? 0;

            OnPropertyChanged(nameof(ActiveOrderCount));
            OnPropertyChanged(nameof(ActiveRevenue));
            OnPropertyChanged(nameof(ActiveAverageTicket));
            OnPropertyChanged(nameof(SecondaryMetricValue));
            OnPropertyChanged(nameof(TopProductName));
            OnPropertyChanged(nameof(TopProductSales));
        }

        private string ActiveOrderNode => IsRegisterTabSelected ? "walk-in-orders" : IsKioskTabSelected ? "kioskSales" : "orders";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
