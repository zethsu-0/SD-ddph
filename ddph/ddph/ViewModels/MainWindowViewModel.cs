using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Runtime.CompilerServices;
using ddph.Data;
using ddph.Models;
using ddph.Receipts;

namespace ddph.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private readonly ProductRepository _productRepository = new();
        private readonly SalesRepository _salesRepository = new();
        private Product? _selectedProduct;
        private string _paymentText = string.Empty;
        private string _searchText = string.Empty;
        private string _selectedCategory = "All";
        private decimal _discountRate;
        private string? _discountCustomerType;

        public MainWindowViewModel()
        {
            Products = new ObservableCollection<Product>();
            Categories = new ObservableCollection<string> { "All" };
            CartItems = new ObservableCollection<CartItem>();
            FilteredProducts = CollectionViewSource.GetDefaultView(Products);
            FilteredProducts.Filter = FilterProducts;
            RefreshProductsCommand = new RelayCommand(_ => LoadProducts());
            ClearCartCommand = new RelayCommand(_ => ClearCart(), _ => CartItems.Any());
            ClearFiltersCommand = new RelayCommand(_ => ClearFilters());
            CheckoutCommand = new RelayCommand(_ => Checkout(), _ => CartItems.Any());
            RemoveCartItemCommand = new RelayCommand(
                parameter => DecreaseCartItemQuantity(parameter as CartItem),
                parameter => parameter is CartItem);
            ProductButtonCommand = new RelayCommand(
                parameter => AddProductToCart(parameter as Product),
                parameter => parameter is Product);

            LoadProducts();
        }

        public ObservableCollection<Product> Products { get; }
        public ObservableCollection<string> Categories { get; }
        public ObservableCollection<CartItem> CartItems { get; }
        public ICollectionView FilteredProducts { get; }

        public int ProductCount => FilteredProducts.Cast<object>().Count();
        public decimal CartSubtotal => CartItems.Sum(item => item.Price * item.Qty);
        public decimal DiscountRate => _discountRate;
        public decimal DiscountAmount => Math.Round(CartSubtotal * (_discountRate / 100m), 2, MidpointRounding.AwayFromZero);
        public decimal CartTotal => CartSubtotal - DiscountAmount;
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

            _discountRate = discountRate;
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

        private void LoadProducts()
        {
            try
            {
                Products.Clear();

                foreach (var product in _productRepository.GetProducts())
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
                    Qty = 1,
                    Price = product.Price
                });
            }

            OnPropertyChanged(nameof(CartTotal));
            RefreshCartTotals();
        }

        private void ClearCart()
        {
            CartItems.Clear();
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

        private void Checkout()
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

            if (!decimal.TryParse(PaymentText, NumberStyles.Number, CultureInfo.InvariantCulture, out var payment))
            {
                MessageBox.Show("Enter a valid payment amount.", "Invalid Payment", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                var cartItems = CartItems.ToList();
                var subtotal = CartSubtotal;
                var discountAmount = DiscountAmount;
                var total = CartTotal;
                var change = payment - total;
                var createdAt = DateTime.Now;
                var saleReference = _salesRepository.CheckoutSale(cartItems, "Staff", payment, _discountRate, _discountCustomerType);
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
                    payment,
                    change,
                    HasDiscount ? $"Discount ({DiscountSummary})" : "Discount",
                    saleReference,
                    createdAt);

                ReceiptGenerated?.Invoke(receipt);
                ClearCart();
                LoadProducts();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Unable to complete checkout.\n\n{ex.Message}", "Checkout Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            OnPropertyChanged(nameof(HasDiscount));
            OnPropertyChanged(nameof(DiscountTypeLabel));
            OnPropertyChanged(nameof(DiscountSummary));
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
