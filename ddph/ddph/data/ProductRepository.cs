using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ddph.Models;

namespace ddph.Data
{
    public class ProductRepository
    {
        private readonly FirebaseDatabaseClient _firebaseClient = new();

        public async Task<List<Product>> GetProductsAsync()
        {
            var products = await _firebaseClient
                .GetAsync<Dictionary<string, FirebaseProductRecord>>("products")
                .ConfigureAwait(false);

            if (products == null)
            {
                return new List<Product>();
            }

            return products
                .Where(entry => entry.Value != null)
                .Select(entry => MapToProduct(entry.Key, entry.Value!))
                .OrderBy(product => product.ProductName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<Product> AddProductAsync(Product product)
        {
            var normalizedCategory = NormalizeCategory(product.Category);
            var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            var payload = new Dictionary<string, object?>
            {
                ["name"] = product.ProductName.Trim(),
                ["category"] = normalizedCategory,
                ["price"] = product.Price,
                ["image"] = product.ImageUrl ?? string.Empty,
                ["createdAt"] = now,
                ["updatedAt"] = now
            };

            var created = await _firebaseClient
                .PostAsync<FirebasePushResponse>("products", payload)
                .ConfigureAwait(false);

            if (created == null || string.IsNullOrWhiteSpace(created.Name))
            {
                throw new InvalidOperationException("Firebase did not return a product key.");
            }

            await EnsureCategoryAsync(normalizedCategory).ConfigureAwait(false);

            product.Id = created.Name;
            product.Category = normalizedCategory;
            product.CreatedAt = now;
            return product;
        }

        public async Task UpdateProductAsync(Product product)
        {
            if (string.IsNullOrWhiteSpace(product.Id))
            {
                throw new InvalidOperationException("The selected product is missing its Firebase key.");
            }

            var existingProduct = await _firebaseClient
                .GetAsync<Dictionary<string, object?>>($"products/{product.Id}")
                .ConfigureAwait(false);

            if (existingProduct == null)
            {
                throw new InvalidOperationException("The selected product no longer exists in Firebase.");
            }

            var normalizedCategory = NormalizeCategory(product.Category);
            existingProduct["name"] = product.ProductName.Trim();
            existingProduct["category"] = normalizedCategory;
            existingProduct["price"] = product.Price;
            existingProduct["image"] = product.ImageUrl ?? string.Empty;
            existingProduct["updatedAt"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            await _firebaseClient
                .PutAsync($"products/{product.Id}", existingProduct)
                .ConfigureAwait(false);

            await EnsureCategoryAsync(normalizedCategory).ConfigureAwait(false);
            product.Category = normalizedCategory;
        }

        public async Task DeleteProductAsync(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId))
            {
                return;
            }

            await _firebaseClient.DeleteAsync($"products/{productId}").ConfigureAwait(false);
        }

        private async Task EnsureCategoryAsync(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName) ||
                categoryName.Equals("Uncategorized", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var categoryKey = ToCategoryKey(categoryName);
            var existingCategory = await _firebaseClient
                .GetAsync<Dictionary<string, object?>>($"categories/{categoryKey}")
                .ConfigureAwait(false);

            if (existingCategory != null)
            {
                return;
            }

            var categoryPayload = new Dictionary<string, object?>
            {
                ["name"] = categoryName,
                ["order"] = 999,
                ["protected"] = false
            };

            await _firebaseClient.PutAsync($"categories/{categoryKey}", categoryPayload).ConfigureAwait(false);
        }

        private static Product MapToProduct(string key, FirebaseProductRecord record)
        {
            return new Product
            {
                Id = key,
                Description = record.Description ?? string.Empty,
                ProductName = record.Name ?? string.Empty,
                ImageUrl = record.Image ?? string.Empty,
                CreatedAt = record.CreatedAt ?? string.Empty,
                Category = NormalizeCategory(record.Category),
                Price = record.Price
            };
        }

        private static string NormalizeCategory(string? category)
        {
            return string.IsNullOrWhiteSpace(category) ? "Uncategorized" : category.Trim();
        }

        private static string ToCategoryKey(string categoryName)
        {
            var chars = categoryName
                .Trim()
                .ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
                .ToArray();

            var key = new string(chars);
            while (key.Contains("--", StringComparison.Ordinal))
            {
                key = key.Replace("--", "-", StringComparison.Ordinal);
            }

            return key.Trim('-');
        }

        private sealed class FirebaseProductRecord
        {
            public string? Category { get; set; }
            public string? CreatedAt { get; set; }
            public string? Description { get; set; }
            public string? Image { get; set; }
            public string? Name { get; set; }
            public decimal Price { get; set; }
        }

        private sealed class FirebasePushResponse
        {
            public string? Name { get; set; }
        }
    }
}
