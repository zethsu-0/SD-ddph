using System.Globalization;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using ddph.Converters;
using ddph.Data;
using ddph.Models;

namespace ddph.Views
{
    public partial class AddProductWindow : Window
    {
        private readonly CloudinaryImageService _cloudinaryImageService = new();

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
            if (!string.IsNullOrWhiteSpace(productToEdit.ImageUrl))
            {
                ImageUrlTextBox.Text = productToEdit.ImageUrl;
                TryLoadImagePreview(productToEdit.ImageUrl);
            }
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

            CreatedProduct ??= new Product();
            var imageSource = SaveImageSource(ImageUrlTextBox.Text.Trim());
            if (imageSource == null)
            {
                return;
            }

            CreatedProduct.ProductName = ProductNameTextBox.Text.Trim();
            CreatedProduct.Price = price;
            CreatedProduct.ImageUrl = imageSource;
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

        private void BrowseImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Product Image",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp|All Files|*.*",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var filePath = dialog.FileName;
            ImageUrlTextBox.Text = filePath;
            TryLoadImagePreview(filePath);
        }

        private void ImageUrlTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var url = ImageUrlTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                ClearImagePreview();
                return;
            }

            TryLoadImagePreview(url);
        }

        private void TryLoadImagePreview(string source)
        {
            var bitmap = ProductImageConverter.CreateImageSource(source);
            if (bitmap == null)
            {
                ClearImagePreview();
                return;
            }

            ImagePreview.Source = bitmap;
            ImagePreview.Visibility = Visibility.Visible;
            ImagePlaceholderText.Visibility = Visibility.Collapsed;
        }

        private void ClearImagePreview()
        {
            ImagePreview.Source = null;
            ImagePreview.Visibility = Visibility.Collapsed;
            ImagePlaceholderText.Visibility = Visibility.Visible;
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

        private string? SaveImageSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source) ||
                source.StartsWith("data:image/", System.StringComparison.OrdinalIgnoreCase) ||
                Uri.TryCreate(source, UriKind.Absolute, out var uri) && !uri.IsFile)
            {
                return source;
            }

            var path = Uri.TryCreate(source, UriKind.Absolute, out uri) && uri.IsFile
                ? uri.LocalPath
                : source;

            if (!File.Exists(path))
            {
                return source;
            }

            try
            {
                return _cloudinaryImageService.UploadProductImage(path);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to upload product image.\n\n{ex.Message}\n\nProduct was not saved.",
                    "Image Upload Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return null;
            }
        }
    }
}
