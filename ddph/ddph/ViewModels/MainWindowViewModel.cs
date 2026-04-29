using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ddph.Data;
using ddph.Models;
using ddph.Receipts;

namespace ddph.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public const int MaxPaymentDigits = 6;
        private const decimal MaxPaymentAmount = 999999m;
        private const decimal MaxDiscountRate = 100m;
        private readonly ProductRepository _productRepository = new();
        private readonly SalesRepository _salesRepository = new();
        private Product? _selectedProduct;
        private string _paymentText = string.Empty;
        private string _searchText = string.Empty;
        private string _selectedCategory = "All";
        private decimal _discountRate;
        private string? _discountCustomerType;
        private bool _isLoading;
        private string? _pendingKioskSaleId;
        private string? _pendingCustomerName;
        private string? _pendingCustomerPhone;

        public MainWindowViewModel()
        {
            Products = new ObservableCollection<Product>();
            Categories = new ObservableCollection<string> { "All" };
            CartItems = new ObservableCollection<CartItem>();
            FilteredProducts = CollectionViewSource.GetDefaultView(Products);
            FilteredProducts.Filter = FilterProducts;
            RefreshProductsCommand = new RelayCommand(async _ => await LoadProductsAsync(), _ => !IsLoading);
            ClearCartCommand = new RelayCommand(_ => ClearCart(), _ => CartItems.Any());
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
            CheckoutCommand = new RelayCommand(async _ => await CheckoutAsync(), _ => CartItems.Any() && !IsLoading);
            RemoveCartItemCommand = new RelayCommand(
                parameter => DecreaseCartItemQuantity(parameter as CartItem),
                parameter => parameter is CartItem);
            ProductButtonCommand = new RelayCommand(
                parameter => AddProductToCart(parameter as Product),
                parameter => parameter is Product && !IsLoading);

            _ = LoadProductsAsync();
        }

        public ObservableCollection<Product> Products { get; }
        public ObservableCollection<string> Categories { get; }
        public ObservableCollection<CartItem> CartItems { get; }
        public ICollectionView FilteredProducts { get; }

        public int ProductCount => FilteredProducts.Cast<object>().Count();
        public decimal CartSubtotal => CartItems.Sum(item => item.Price * item.Qty);
        public decimal DiscountRate => _discountRate;
        public decimal DiscountAmount => Math.Round(CartSubtotal * (Math.Min(_discountRate, MaxDiscountRate) / 100m), 2, MidpointRounding.AwayFromZero);
        public decimal CartTotal => CartSubtotal - DiscountAmount;
        public decimal VatableSales => Math.Round(CartTotal * 0.88m, 2, MidpointRounding.AwayFromZero);
        public decimal VatAmount => Math.Round(CartTotal * 0.12m, 2, MidpointRounding.AwayFromZero);
        public bool HasDiscount => _discountRate > 0;
        public string DiscountTypeLabel => string.Equals(_discountCustomerType, "pwd", StringComparison.OrdinalIgnoreCase)
            ? "PWD"
            : string.Equals(_discountCustomerType, "senior", StringComparison.OrdinalIgnoreCase)
                ? "Senior"
                : string.Empty;
        public string DiscountSummary => HasDiscount
            ? string.IsNullOrWhiteSpace(DiscountTypeLabel)
                ? $"{_discountRate:0.##}% off"
                : $"{_discountRate:0.##}% off ({DiscountTypeLabel})"
            : "Add note or discount";

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
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public Product? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                _selectedProduct = value;
                OnPropertyChanged();
            }
        }

        public string PaymentText
        {
            get => _paymentText;
            set
            {
                if (_paymentText == value)
                {
                    return;
                }

                _paymentText = value;
                OnPropertyChanged();
            }
        }

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
                RefreshFilters();
            }
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (_selectedCategory == value)
                {
                    return;
                }

                _selectedCategory = value;
                OnPropertyChanged();
                RefreshFilters();
            }
        }

        public ICommand RefreshProductsCommand { get; }
        public ICommand ClearCartCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand CheckoutCommand { get; }
        public ICommand RemoveCartItemCommand { get; }
        public ICommand ProductButtonCommand { get; }
        public event Action? PaymentFocusRequested;
        public event Action<ReceiptPdfResult>? ReceiptGenerated;

        public void ApplyDiscount(decimal discountRate, string? customerType)
        {
            if (discountRate <= 0)
            {
                ClearDiscount();
                return;
            }

            _discountRate = Math.Min(discountRate, MaxDiscountRate);
            _discountCustomerType = string.IsNullOrWhiteSpace(customerType) || string.Equals(customerType, "discount", StringComparison.OrdinalIgnoreCase)
                ? null
                : customerType.Trim();
            RefreshCartTotals();
        }

        public void ClearDiscount()
        {
            _discountRate = 0;
            _discountCustomerType = null;
            RefreshCartTotals();
        }

        public void UpdateCartItemQuantity(CartItem? cartItem, int quantity)
        {
            if (cartItem == null || quantity <= 0)
            {
                return;
            }

            var existingCartItem = CartItems.FirstOrDefault(item => item.ProductId == cartItem.ProductId);
            if (existingCartItem == null)
            {
                return;
            }

            var existingIndex = CartItems.IndexOf(existingCartItem);
            CartItems[existingIndex] = new CartItem
            {
                ProductId = existingCartItem.ProductId,
                Item = existingCartItem.Item,
                Category = existingCartItem.Category,
                Qty = quantity,
                Price = existingCartItem.Price
            };

            RefreshCartTotals();
        }

        public void RemoveCartItem(CartItem? cartItem)
        {
            if (cartItem == null)
            {
                return;
            }

            var existingCartItem = CartItems.FirstOrDefault(item => item.ProductId == cartItem.ProductId);
            if (existingCartItem == null)
            {
                return;
            }

            CartItems.Remove(existingCartItem);
            RefreshCartTotals();
        }

        public void AddOrderToCart(OnlineOrder? order)
        {
            if (order == null)
            {
                return;
            }

            ClearCart();
            _pendingKioskSaleId = string.Equals(order.SourceLabel, "Kiosk", StringComparison.OrdinalIgnoreCase)
                ? order.Id
                : null;
            _pendingCustomerName = _pendingKioskSaleId == null
                ? null
                : order.DisplayName;
            _pendingCustomerPhone = _pendingKioskSaleId == null
                ? null
                : order.CustomerPhone;

            foreach (var item in order.Items)
            {
                AddOrderItemToCart(item);
            }

            RefreshCartTotals();
        }

        private async Task LoadProductsAsync()
        {
            if (IsLoading)
            {
                return;
            }

            try
            {
                IsLoading = true;
                await LoadProductsCoreAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadProductsCoreAsync()
        {
            try
            {
                Products.Clear();

                foreach (var product in await _productRepository.GetProductsAsync())
                {
                    Products.Add(product);
                }

                RebuildCategories();
                RefreshFilters();
                OnPropertyChanged(nameof(ProductCount));
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"Unable to load products into the main screen.\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void AddProductToCart(Product? product)
        {
            if (product == null)
            {
                return;
            }

            SelectedProduct = product;

            var existingCartItem = CartItems.FirstOrDefault(item => item.ProductId == product.Id);
            if (existingCartItem != null)
            {
                var existingIndex = CartItems.IndexOf(existingCartItem);
                CartItems[existingIndex] = new CartItem
                {
                    ProductId = existingCartItem.ProductId,
                    Item = existingCartItem.Item,
                    Category = existingCartItem.Category,
                    Qty = existingCartItem.Qty + 1,
                    Price = existingCartItem.Price
                };
            }
            else
            {
                CartItems.Add(new CartItem
                {
                    ProductId = product.Id,
                    Item = product.ProductName,
                    Category = product.Category,
                    Qty = 1,
                    Price = product.Price
                });
            }

            OnPropertyChanged(nameof(CartTotal));
            RefreshCartTotals();
        }

        private void AddOrderItemToCart(OnlineOrderItem item)
        {
            var productId = string.IsNullOrWhiteSpace(item.ProductId)
                ? $"order-item:{item.Name}:{item.Category}:{item.Price}"
                : item.ProductId;
            var quantity = Math.Max(1, item.Quantity);
            var existingCartItem = CartItems.FirstOrDefault(cartItem => cartItem.ProductId == productId);

            if (existingCartItem != null)
            {
                var existingIndex = CartItems.IndexOf(existingCartItem);
                CartItems[existingIndex] = new CartItem
                {
                    ProductId = existingCartItem.ProductId,
                    Item = existingCartItem.Item,
                    Category = existingCartItem.Category,
                    Qty = existingCartItem.Qty + quantity,
                    Price = existingCartItem.Price
                };
                return;
            }

            CartItems.Add(new CartItem
            {
                ProductId = productId,
                Item = item.Name,
                Category = item.Category,
                Qty = quantity,
                Price = item.Price
            });
        }

        private void ClearCart()
        {
            CartItems.Clear();
            _pendingKioskSaleId = null;
            _pendingCustomerName = null;
            _pendingCustomerPhone = null;
            PaymentText = string.Empty;
            ClearDiscount();
            RefreshCartTotals();
        }

        private void DecreaseCartItemQuantity(CartItem? cartItem)
        {
            if (cartItem == null)
            {
                return;
            }

            var existingCartItem = CartItems.FirstOrDefault(item => item.ProductId == cartItem.ProductId);
            if (existingCartItem == null)
            {
                return;
            }

            var existingIndex = CartItems.IndexOf(existingCartItem);
            if (existingCartItem.Qty > 1)
            {
                CartItems[existingIndex] = new CartItem
                {
                    ProductId = existingCartItem.ProductId,
                    Item = existingCartItem.Item,
                    Category = existingCartItem.Category,
                    Qty = existingCartItem.Qty - 1,
                    Price = existingCartItem.Price
                };
            }
            else
            {
                CartItems.Remove(existingCartItem);
            }

            RefreshCartTotals();
        }

        private bool FilterProducts(object item)
        {
            if (item is not Product product)
            {
                return false;
            }

            if (!string.Equals(SelectedCategory, "All", System.StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(product.Category, SelectedCategory, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(SearchText) &&
                !product.ProductName.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private void ClearFilters()
        {
            SearchText = string.Empty;
            SelectedCategory = "All";
            RefreshFilters();
        }

        private void RefreshFilters()
        {
            FilteredProducts.Refresh();
            OnPropertyChanged(nameof(ProductCount));
        }

        private void RebuildCategories()
        {
            var categories = Products
                .Select(product => string.IsNullOrWhiteSpace(product.Category) ? "Uncategorized" : product.Category)
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .OrderBy(category => category, System.StringComparer.OrdinalIgnoreCase)
                .ToList();

            Categories.Clear();
            Categories.Add("All");

            foreach (var category in categories)
            {
                Categories.Add(category);
            }

            if (!Categories.Contains(SelectedCategory))
            {
                _selectedCategory = "All";
                OnPropertyChanged(nameof(SelectedCategory));
            }
        }

        private async Task CheckoutAsync()
        {
            if (!CartItems.Any())
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(PaymentText))
            {
                MessageBox.Show("Enter the payment amount first.", "Payment Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                PaymentFocusRequested?.Invoke();
                return;
            }

            if (!decimal.TryParse(PaymentText, NumberStyles.Number, CultureInfo.CurrentCulture, out var payment))
            {
                MessageBox.Show("Enter a valid payment amount.", "Invalid Payment", MessageBoxButton.OK, MessageBoxImage.Warning);
                PaymentFocusRequested?.Invoke();
                return;
            }

            if (payment <= 0)
            {
                MessageBox.Show("Payment must be greater than zero.", "Invalid Payment", MessageBoxButton.OK, MessageBoxImage.Warning);
                PaymentFocusRequested?.Invoke();
                return;
            }

            if (payment > MaxPaymentAmount)
            {
                MessageBox.Show($"Payment must be {MaxPaymentDigits} digits or less.", "Invalid Payment", MessageBoxButton.OK, MessageBoxImage.Warning);
                PaymentFocusRequested?.Invoke();
                return;
            }

            if (payment < CartTotal)
            {
                MessageBox.Show("Payment is less than the total amount.", "Insufficient Payment", MessageBoxButton.OK, MessageBoxImage.Warning);
                PaymentFocusRequested?.Invoke();
                return;
            }

            try
            {
                IsLoading = true;
                var cartItems = CartItems.ToList();
                var subtotal = CartSubtotal;
                var discountAmount = DiscountAmount;
                var total = CartTotal;
                var vatableSales = VatableSales;
                var vatAmount = VatAmount;
                var change = payment - total;
                var createdAt = DateTime.Now;
                var saleReference = await _salesRepository.CheckoutSaleAsync(cartItems, AuthSessionStore.CurrentUsername, payment, _discountRate, _discountCustomerType, _pendingKioskSaleId, _pendingCustomerName, _pendingCustomerPhone);
                var receiptDirectory = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "DDPH Receipts");
                Directory.CreateDirectory(receiptDirectory);

                var receiptPath = System.IO.Path.Combine(receiptDirectory, $"ddph-receipt-{saleReference}-{createdAt:yyyyMMdd-HHmmss}.png");
                var receipt = ReceiptPdfService.Generate(
                    receiptPath,
                    cartItems,
                    subtotal,
                    discountAmount,
                    total,
                    vatableSales,
                    vatAmount,
                    payment,
                    change,
                    HasDiscount ? $"Discount ({DiscountSummary})" : "Discount",
                    saleReference,
                    createdAt);

                ReceiptGenerated?.Invoke(receipt);
                ClearCart();
                await LoadProductsCoreAsync();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Unable to complete checkout.\n\n{ex.Message}", "Checkout Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void RefreshCartTotals()
        {
            OnPropertyChanged(nameof(CartSubtotal));
            OnPropertyChanged(nameof(DiscountRate));
            OnPropertyChanged(nameof(DiscountAmount));
            OnPropertyChanged(nameof(CartTotal));
            OnPropertyChanged(nameof(VatableSales));
            OnPropertyChanged(nameof(VatAmount));
            OnPropertyChanged(nameof(HasDiscount));
            OnPropertyChanged(nameof(DiscountTypeLabel));
            OnPropertyChanged(nameof(DiscountSummary));
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
