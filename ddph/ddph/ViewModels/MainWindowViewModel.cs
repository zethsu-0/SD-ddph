using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Runtime.CompilerServices;
using ddph.Data;
using ddph.Models;

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
        private bool _showInStockOnly;

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
                parameter => RemoveCartItem(parameter as CartItem),
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
        public decimal CartTotal => CartItems.Sum(item => item.Price * item.Qty);

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

        public bool ShowInStockOnly
        {
            get => _showInStockOnly;
            set
            {
                if (_showInStockOnly == value)
                {
                    return;
                }

                _showInStockOnly = value;
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
            CommandManager.InvalidateRequerySuggested();
        }

        private void ClearCart()
        {
            CartItems.Clear();
            PaymentText = string.Empty;
            OnPropertyChanged(nameof(CartTotal));
            CommandManager.InvalidateRequerySuggested();
        }

        private void RemoveCartItem(CartItem? cartItem)
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

            OnPropertyChanged(nameof(CartTotal));
            CommandManager.InvalidateRequerySuggested();
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

            if (ShowInStockOnly && product.Stock.GetValueOrDefault() <= 0)
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
            ShowInStockOnly = false;
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
                _salesRepository.CheckoutSale(CartItems.ToList(), "Staff", payment);
                MessageBox.Show("Checkout completed successfully.", "Checkout", MessageBoxButton.OK, MessageBoxImage.Information);
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
    }
}
