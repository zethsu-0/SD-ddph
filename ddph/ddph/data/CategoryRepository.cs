using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ddph.Data
{
    public class CategoryRepository
    {
        private const string Uncategorized = "Uncategorized";
        private readonly IFirebaseDatabaseClient _firebaseClient;

        public CategoryRepository()
            : this(new FirebaseDatabaseClient())
        {
        }

        public CategoryRepository(IFirebaseDatabaseClient firebaseClient)
        {
            _firebaseClient = firebaseClient;
        }

        public async Task<List<string>> GetCategoriesAsync()
        {
            var categories = await _firebaseClient
                .GetAsync<Dictionary<string, Dictionary<string, object?>>>("categories")
                .ConfigureAwait(false);

            var names = categories?
                .Select(entry => ReadString(entry.Value, "name"))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => NormalizeName(name!))
                .ToList() ?? new List<string>();

            if (!names.Contains(Uncategorized, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(Uncategorized);
            }

            return names
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task AddCategoryAsync(string name)
        {
            var normalizedName = NormalizeName(name);
            var key = ToCategoryKey(normalizedName);

            if (normalizedName.Equals(Uncategorized, StringComparison.OrdinalIgnoreCase) ||
                await CategoryExistsAsync(key).ConfigureAwait(false))
            {
                throw new InvalidOperationException("Category already exists.");
            }

            await _firebaseClient
                .PutAsync($"categories/{key}", CreatePayload(normalizedName))
                .ConfigureAwait(false);
        }

        public async Task RenameCategoryAsync(string oldName, string newName)
        {
            var oldCategoryName = NormalizeName(oldName);
            var newCategoryName = NormalizeName(newName);
            if (oldCategoryName.Equals(Uncategorized, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Uncategorized cannot be renamed.");
            }

            if (newCategoryName.Equals(Uncategorized, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Category already exists.");
            }

            var oldKey = ToCategoryKey(oldCategoryName);
            var newKey = ToCategoryKey(newCategoryName);

            var existingCategory = await _firebaseClient
                .GetAsync<Dictionary<string, object?>>($"categories/{oldKey}")
                .ConfigureAwait(false);

            if (existingCategory == null)
            {
                throw new InvalidOperationException("Category no longer exists.");
            }

            if (!string.Equals(oldKey, newKey, StringComparison.Ordinal) &&
                await CategoryExistsAsync(newKey).ConfigureAwait(false))
            {
                throw new InvalidOperationException("Category already exists.");
            }

            existingCategory["name"] = newCategoryName;
            existingCategory["updatedAt"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            await _firebaseClient.PutAsync($"categories/{newKey}", existingCategory).ConfigureAwait(false);
            if (!string.Equals(oldKey, newKey, StringComparison.Ordinal))
            {
                await _firebaseClient.DeleteAsync($"categories/{oldKey}").ConfigureAwait(false);
            }

            await UpdateProductCategoriesAsync(oldCategoryName, newCategoryName).ConfigureAwait(false);
        }

        public async Task DeleteCategoryAsync(string name)
        {
            var normalizedName = NormalizeName(name);
            if (normalizedName.Equals(Uncategorized, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Uncategorized cannot be removed.");
            }

            if (await IsCategoryUsedAsync(normalizedName).ConfigureAwait(false))
            {
                throw new InvalidOperationException("Category is used by products.");
            }

            await _firebaseClient
                .DeleteAsync($"categories/{ToCategoryKey(normalizedName)}")
                .ConfigureAwait(false);
        }

        private async Task<bool> CategoryExistsAsync(string key)
        {
            var category = await _firebaseClient
                .GetAsync<Dictionary<string, object?>>($"categories/{key}")
                .ConfigureAwait(false);

            return category != null;
        }

        private async Task<bool> IsCategoryUsedAsync(string categoryName)
        {
            var products = await GetProductsAsync().ConfigureAwait(false);
            return products.Any(product => string.Equals(
                ReadString(product.Value, "category"),
                categoryName,
                StringComparison.OrdinalIgnoreCase));
        }

        private async Task UpdateProductCategoriesAsync(string oldName, string newName)
        {
            var products = await GetProductsAsync().ConfigureAwait(false);
            foreach (var product in products.Where(product => string.Equals(
                ReadString(product.Value, "category"),
                oldName,
                StringComparison.OrdinalIgnoreCase)))
            {
                await _firebaseClient
                    .PatchAsync($"products/{product.Key}", new Dictionary<string, object?>
                    {
                        ["category"] = newName
                    })
                    .ConfigureAwait(false);
            }
        }

        private async Task<Dictionary<string, Dictionary<string, object?>>> GetProductsAsync()
        {
            return await _firebaseClient
                .GetAsync<Dictionary<string, Dictionary<string, object?>>>("products")
                .ConfigureAwait(false) ?? new Dictionary<string, Dictionary<string, object?>>();
        }

        private static Dictionary<string, object?> CreatePayload(string name)
        {
            var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            return new Dictionary<string, object?>
            {
                ["name"] = name,
                ["order"] = 999,
                ["protected"] = false,
                ["createdAt"] = now,
                ["updatedAt"] = now
            };
        }

        private static string NormalizeName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Category name is required.");
            }

            return name.Trim();
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

        private static string? ReadString(Dictionary<string, object?> values, string key)
        {
            if (!values.TryGetValue(key, out var value))
            {
                return null;
            }

            return value switch
            {
                string text => text,
                JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
                JsonElement element => element.ToString(),
                _ => value?.ToString()
            };
        }
    }
}
