using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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
        private string _searchText = string.Empty;
        private string _selectedCategory = "All Categories";
        private Product? _selectedProduct;

        public InventoryViewModel()
        {
            Products = new ObservableCollection<Product>();
            CategorySummaries = new ObservableCollection<CategorySummary>();
            Categories = new ObservableCollection<string> { "All Categories" };

            FilteredProducts = CollectionViewSource.GetDefaultView(Products);
            FilteredProducts.Filter = FilterProducts;

            AddProductCommand = new RelayCommand(_ => AddProduct());
            EditProductCommand = new RelayCommand(
                parameter => EditProduct(parameter as Product),
                parameter => parameter is Product);
            DeleteProductCommand = new RelayCommand(
                parameter => DeleteProduct(parameter as Product),
                parameter => parameter is Product);

            LoadProducts();
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

        public int VisibleProductCount => FilteredProducts.Cast<object>().Count();

        public ICommand AddProductCommand { get; }
        public ICommand EditProductCommand { get; }
        public ICommand DeleteProductCommand { get; }

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

        private void AddProduct()
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

                var savedProduct = _productRepository.AddProduct(addProductWindow.CreatedProduct);
                Products.Add(savedProduct);
                SelectedProduct = savedProduct;
                RebuildCategoryData();
                FilteredProducts.Refresh();
                OnPropertyChanged(nameof(VisibleProductCount));
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Unable to add product.\n\n{ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditProduct(Product? product)
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
                _productRepository.UpdateProduct(updatedProduct);

                product.ProductName = updatedProduct.ProductName;
                product.Price = updatedProduct.Price;
                product.ImageUrl = updatedProduct.ImageUrl;
                product.Category = updatedProduct.Category;

                RebuildCategoryData();
                FilteredProducts.Refresh();
                OnPropertyChanged(nameof(VisibleProductCount));
                MessageBox.Show("Product changes were saved to the database.", "Product Updated", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Unable to update product.\n\n{ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteProduct(Product? product)
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
                _productRepository.DeleteProduct(product.Id);
                Products.Remove(product);

                if (SelectedProduct == product)
                {
                    SelectedProduct = null;
                }

                RebuildCategoryData();
                FilteredProducts.Refresh();
                OnPropertyChanged(nameof(VisibleProductCount));
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Unable to delete product.\n\n{ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

                RebuildCategoryData();
                FilteredProducts.Refresh();
                OnPropertyChanged(nameof(VisibleProductCount));
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"Unable to load products from the database.\n\n{ex.Message}",
                    "Database Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private bool MatchesCategory(Product product)
        {
            return string.Equals(SelectedCategory, "All Categories", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(product.Category, SelectedCategory, System.StringComparison.OrdinalIgnoreCase);
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

            foreach (var group in categoryCounts)
            {
                Categories.Add(group.Key);
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
                    Icon = GetCategoryIcon(group.Key)
                });
            }
        }

        private static string GetCategoryIcon(string category)
        {
            return category.ToLowerInvariant() switch
            {
                "cakes" => "🎂",
                "cookies" => "🍪",
                "cupcakes" => "🧁",
                _ => "🍽"
            };
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
