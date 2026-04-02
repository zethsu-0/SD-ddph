using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ddph.Models;

namespace ddph.Data
{
    public class ProductRepository
    {
        private readonly FirebaseDatabaseClient _firebaseClient = new();

        public List<Product> GetProducts()
        {
            var products = _firebaseClient
                .GetAsync<Dictionary<string, FirebaseProductRecord>>("products")
                .GetAwaiter()
                .GetResult();

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

        public Product AddProduct(Product product)
        {
            var normalizedCategory = NormalizeCategory(product.Category);
            var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            var payload = new Dictionary<string, object?>
            {
                ["name"] = product.ProductName.Trim(),
                ["category"] = normalizedCategory,
                ["price"] = product.Price,
                ["createdAt"] = now,
                ["updatedAt"] = now
            };

            if (product.Stock.HasValue)
            {
                payload["stock"] = product.Stock.Value;
            }

            var created = _firebaseClient
                .PostAsync<FirebasePushResponse>("products", payload)
                .GetAwaiter()
                .GetResult();

            if (created == null || string.IsNullOrWhiteSpace(created.Name))
            {
                throw new InvalidOperationException("Firebase did not return a product key.");
            }

            EnsureCategory(normalizedCategory);

            product.Id = created.Name;
            product.Category = normalizedCategory;
            return product;
        }

        public void UpdateProduct(Product product)
        {
            if (string.IsNullOrWhiteSpace(product.Id))
            {
                throw new InvalidOperationException("The selected product is missing its Firebase key.");
            }

            var existingProduct = _firebaseClient
                .GetAsync<Dictionary<string, object?>>($"products/{product.Id}")
                .GetAwaiter()
                .GetResult();

            if (existingProduct == null)
            {
                throw new InvalidOperationException("The selected product no longer exists in Firebase.");
            }

            var normalizedCategory = NormalizeCategory(product.Category);
            existingProduct["name"] = product.ProductName.Trim();
            existingProduct["category"] = normalizedCategory;
            existingProduct["price"] = product.Price;
            existingProduct["updatedAt"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            if (product.Stock.HasValue)
            {
                existingProduct["stock"] = product.Stock.Value;
            }
            else
            {
                existingProduct.Remove("stock");
            }

            _firebaseClient
                .PutAsync($"products/{product.Id}", existingProduct)
                .GetAwaiter()
                .GetResult();

            EnsureCategory(normalizedCategory);
            product.Category = normalizedCategory;
        }

        public void DeleteProduct(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId))
            {
                return;
            }

            _firebaseClient.DeleteAsync($"products/{productId}").GetAwaiter().GetResult();
        }

        private void EnsureCategory(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName) ||
                categoryName.Equals("Uncategorized", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var categoryKey = ToCategoryKey(categoryName);
            var existingCategory = _firebaseClient
                .GetAsync<Dictionary<string, object?>>($"categories/{categoryKey}")
                .GetAwaiter()
                .GetResult();

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

            _firebaseClient.PutAsync($"categories/{categoryKey}", categoryPayload).GetAwaiter().GetResult();
        }

        private static Product MapToProduct(string key, FirebaseProductRecord record)
        {
            return new Product
            {
                Id = key,
                Description = record.Description ?? string.Empty,
                ProductName = record.Name ?? string.Empty,
                ImageUrl = record.Image ?? string.Empty,
                Category = NormalizeCategory(record.Category),
                Price = record.Price,
                Stock = record.Stock
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
            public string? Description { get; set; }
            public string? Image { get; set; }
            public string? Name { get; set; }
            public decimal Price { get; set; }
            public int? Stock { get; set; }
        }

        private sealed class FirebasePushResponse
        {
            public string? Name { get; set; }
        }
    }
}
