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
using ddph.Views;

namespace ddph.ViewModels
{
    public class InventoryViewModel : INotifyPropertyChanged
    {
        private readonly ProductRepository _productRepository = new();
        private readonly CategoryRepository _categoryRepository = new();
        private List<string> _savedCategories = new();
        private string _searchText = string.Empty;
        private string _selectedCategory = "All Categories";
        private string _sortProperty = string.Empty;
        private ListSortDirection _sortDirection = ListSortDirection.Ascending;
        private Product? _selectedProduct;
        private bool _isLoading;

        public InventoryViewModel()
            : this(true)
        {
        }

        public InventoryViewModel(bool loadProducts)
        {
            Products = new ObservableCollection<Product>();
            CategorySummaries = new ObservableCollection<CategorySummary>();
            Categories = new ObservableCollection<string> { "All Categories" };

            FilteredProducts = CollectionViewSource.GetDefaultView(Products);
            FilteredProducts.Filter = FilterProducts;

            AddProductCommand = new RelayCommand(async _ => await AddProductAsync(), _ => !IsLoading);
            EditProductCommand = new RelayCommand(
                async parameter => await EditProductAsync(parameter as Product),
                parameter => parameter is Product && !IsLoading);
            DeleteProductCommand = new RelayCommand(
                async parameter => await DeleteProductAsync(parameter as Product),
                parameter => parameter is Product && !IsLoading);
            SortProductsCommand = new RelayCommand(SortProducts);

            if (loadProducts)
            {
                _ = LoadProductsAsync();
            }
        }

        public ObservableCollection<Product> Products { get; }
        public ObservableCollection<CategorySummary> CategorySummaries { get; }
        public ObservableCollection<string> Categories { get; }

        public ICollectionView FilteredProducts { get; }

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
                FilteredProducts.Refresh();
                OnPropertyChanged(nameof(VisibleProductCount));
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
                FilteredProducts.Refresh();
                OnPropertyChanged(nameof(VisibleProductCount));
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

        public Product? LatestProduct => Products
            .OrderByDescending(GetProductTimestamp)
            .ThenByDescending(product => product.Id, System.StringComparer.Ordinal)
            .FirstOrDefault();

        public int VisibleProductCount => FilteredProducts.Cast<object>().Count();

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

        public ICommand AddProductCommand { get; }
        public ICommand EditProductCommand { get; }
        public ICommand DeleteProductCommand { get; }
        public ICommand SortProductsCommand { get; }

        private bool FilterProducts(object item)
        {
            if (item is not Product product)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return MatchesCategory(product);
            }

            return MatchesCategory(product) &&
                (product.ProductName.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ||
                product.Category.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase) ||
                product.Description.Contains(SearchText, System.StringComparison.OrdinalIgnoreCase));
        }

        private void SortProducts(object? parameter)
        {
            if (parameter is not string propertyName || string.IsNullOrWhiteSpace(propertyName))
            {
                return;
            }

            if (_sortProperty == propertyName)
            {
                _sortDirection = _sortDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;
            }
            else
            {
                _sortProperty = propertyName;
                _sortDirection = ListSortDirection.Ascending;
            }

            ApplyProductSort();
        }

        private void ApplyProductSort()
        {
            FilteredProducts.SortDescriptions.Clear();
            FilteredProducts.SortDescriptions.Add(new SortDescription(_sortProperty, _sortDirection));
            FilteredProducts.Refresh();
        }

        private async Task AddProductAsync()
        {
            try
            {
                var addProductWindow = new AddProductWindow(GetEditableCategories())
                {
                    Owner = Application.Current.MainWindow
                };

                if (addProductWindow.ShowDialog() != true || addProductWindow.CreatedProduct == null)
                {
                    return;
                }

                IsLoading = true;
                var savedProduct = await _productRepository.AddProductAsync(addProductWindow.CreatedProduct);
                Products.Add(savedProduct);
                SelectedProduct = savedProduct;
                RebuildCategoryData();
                FilteredProducts.Refresh();
                OnPropertyChanged(nameof(VisibleProductCount));
                OnPropertyChanged(nameof(LatestProduct));
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Unable to add product.\n\n{ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task EditProductAsync(Product? product)
        {
            if (product == null)
            {
                return;
            }

            try
            {
                var editProductWindow = new AddProductWindow(product, GetEditableCategories())
                {
                    Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window is Products)
                        ?? Application.Current.MainWindow
                };

                if (editProductWindow.ShowDialog() != true || editProductWindow.CreatedProduct == null)
                {
                    return;
                }

                var updatedProduct = editProductWindow.CreatedProduct;
                IsLoading = true;
                await _productRepository.UpdateProductAsync(updatedProduct);

                product.ProductName = updatedProduct.ProductName;
                product.Price = updatedProduct.Price;
                product.ImageUrl = updatedProduct.ImageUrl;
                product.Category = updatedProduct.Category;

                RebuildCategoryData();
                FilteredProducts.Refresh();
                OnPropertyChanged(nameof(VisibleProductCount));
                OnPropertyChanged(nameof(LatestProduct));
                MessageBox.Show("Product changes were saved to the database.", "Product Updated", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Unable to update product.\n\n{ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DeleteProductAsync(Product? product)
        {
            if (product == null)
            {
                return;
            }

            var result = MessageBox.Show(
                $"Delete '{product.ProductName}' from the database?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                IsLoading = true;
                await _productRepository.DeleteProductAsync(product.Id);
                Products.Remove(product);

                if (SelectedProduct == product)
                {
                    SelectedProduct = null;
                }

                RebuildCategoryData();
                FilteredProducts.Refresh();
                OnPropertyChanged(nameof(VisibleProductCount));
                OnPropertyChanged(nameof(LatestProduct));
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Unable to delete product.\n\n{ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadProductsAsync()
        {
            try
            {
                IsLoading = true;
                Products.Clear();

                foreach (var product in await _productRepository.GetProductsAsync())
                {
                    Products.Add(product);
                }

                _savedCategories = await _categoryRepository.GetCategoriesAsync();
                RebuildCategoryData();
                FilteredProducts.Refresh();
                OnPropertyChanged(nameof(VisibleProductCount));
                OnPropertyChanged(nameof(LatestProduct));
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"Unable to load products from the database.\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool MatchesCategory(Product product)
        {
            return string.Equals(SelectedCategory, "All Categories", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(product.Category, SelectedCategory, System.StringComparison.OrdinalIgnoreCase);
        }

        private static System.DateTimeOffset GetProductTimestamp(Product product)
        {
            if (System.DateTimeOffset.TryParse(
                product.CreatedAt,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var createdAt))
            {
                return createdAt.ToUniversalTime();
            }

            return System.DateTimeOffset.MinValue;
        }

        private void RebuildCategoryData()
        {
            var categoryCounts = Products
                .GroupBy(product => string.IsNullOrWhiteSpace(product.Category) ? "Uncategorized" : product.Category)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key, System.StringComparer.OrdinalIgnoreCase)
                .ToList();

            Categories.Clear();
            Categories.Add("All Categories");

            var categoryNames = categoryCounts
                .Select(group => group.Key)
                .Concat(_savedCategories)
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .OrderBy(category => category, System.StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var category in categoryNames)
            {
                Categories.Add(category);
            }

            if (!Categories.Contains(SelectedCategory))
            {
                _selectedCategory = "All Categories";
                OnPropertyChanged(nameof(SelectedCategory));
            }

            CategorySummaries.Clear();
            CategorySummaries.Add(new CategorySummary
            {
                Title = "Total Items",
                Count = Products.Count,
                Icon = "📦"
            });

            foreach (var group in categoryCounts.Take(3))
            {
                CategorySummaries.Add(new CategorySummary
                {
                    Title = group.Key,
                    Count = group.Count(),
                    Icon = "🍽"
                });
            }
        }

        private string[] GetEditableCategories()
        {
            return Categories
                .Where(category => !string.Equals(category, "All Categories", System.StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
