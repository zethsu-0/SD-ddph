using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ddph.Models;

namespace ddph.Views
{
    public partial class AddProductWindow : Window
    {
        public AddProductWindow(IEnumerable<string>? categories = null)
        {
            InitializeComponent();
            LoadCategories(categories);
        }

        public AddProductWindow(Product productToEdit, IEnumerable<string>? categories = null) : this(categories)
        {
            Title = "Edit Product";
            WindowTitleTextBlock.Text = "Edit Product";
            WindowSubtitleTextBlock.Text = "Update the product details below.";
            SaveButton.Content = "Update";

            ProductNameTextBox.Text = productToEdit.ProductName;
            PriceTextBox.Text = productToEdit.Price.ToString(CultureInfo.InvariantCulture);
            StockTextBox.Text = productToEdit.Stock?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            EnsureCategoryOption(productToEdit.Category);
            CategoryComboBox.SelectedItem = string.IsNullOrWhiteSpace(productToEdit.Category)
                ? "Uncategorized"
                : productToEdit.Category;

            CreatedProduct = new Product
            {
                Id = productToEdit.Id
            };
        }

        public Product? CreatedProduct { get; private set; }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ProductNameTextBox.Text))
            {
                MessageBox.Show("Product name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                ProductNameTextBox.Focus();
                return;
            }

            if (!decimal.TryParse(PriceTextBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var price) || price < 0)
            {
                MessageBox.Show("Enter a valid price.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                PriceTextBox.Focus();
                return;
            }

            int? stock = null;
            if (!string.IsNullOrWhiteSpace(StockTextBox.Text))
            {
                if (!int.TryParse(StockTextBox.Text, out var parsedStock) || parsedStock < 0)
                {
                    MessageBox.Show("Enter a valid stock quantity.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    StockTextBox.Focus();
                    return;
                }

                stock = parsedStock;
            }

            CreatedProduct ??= new Product();
            CreatedProduct.ProductName = ProductNameTextBox.Text.Trim();
            CreatedProduct.Price = price;
            CreatedProduct.Stock = stock;
            CreatedProduct.Category = string.IsNullOrWhiteSpace(CategoryComboBox.Text)
                ? "Uncategorized"
                : CategoryComboBox.Text.Trim();

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void LoadCategories(IEnumerable<string>? categories)
        {
            var normalizedCategories = (categories ?? Enumerable.Empty<string>())
                .Where(category => !string.IsNullOrWhiteSpace(category) &&
                    !string.Equals(category, "All Categories", System.StringComparison.OrdinalIgnoreCase))
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .OrderBy(category => category, System.StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!normalizedCategories.Contains("Uncategorized", System.StringComparer.OrdinalIgnoreCase))
            {
                normalizedCategories.Add("Uncategorized");
            }

            normalizedCategories = normalizedCategories
                .OrderBy(category => category, System.StringComparer.OrdinalIgnoreCase)
                .ToList();

            CategoryComboBox.ItemsSource = normalizedCategories;
            CategoryComboBox.SelectedItem = normalizedCategories.FirstOrDefault();
        }

        private void EnsureCategoryOption(string? category)
        {
            if (string.IsNullOrWhiteSpace(category) || CategoryComboBox.ItemsSource is not IEnumerable<string> categories)
            {
                return;
            }

            if (categories.Contains(category, System.StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            var updatedCategories = categories
                .Append(category)
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, System.StringComparer.OrdinalIgnoreCase)
                .ToList();

            CategoryComboBox.ItemsSource = updatedCategories;
        }
    }
}
