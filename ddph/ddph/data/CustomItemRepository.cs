using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ddph.Models;

namespace ddph.Data
{
    public class CustomItemRepository
    {
        private readonly FirebaseDatabaseClient _firebaseClient = new();

        public List<CustomItem> GetCustomItems()
        {
            var items = _firebaseClient
                .GetAsync<Dictionary<string, FirebaseCustomItemRecord>>("customItems")
                .GetAwaiter()
                .GetResult();

            if (items == null)
            {
                return new List<CustomItem>();
            }

            return items
                .Where(entry => entry.Value != null)
                .Select(entry => new CustomItem
                {
                    Id = entry.Key,
                    Name = entry.Value!.Name ?? string.Empty,
                    Description = entry.Value.Description ?? string.Empty,
                    Image = entry.Value.Image ?? string.Empty,
                    Notes = entry.Value.Notes ?? string.Empty,
                    Type = string.IsNullOrWhiteSpace(entry.Value.Type) ? "custom" : entry.Value.Type!
                })
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public CustomItem AddCustomItem(CustomItem item)
        {
            var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            var payload = new Dictionary<string, object?>
            {
                ["name"] = item.Name.Trim(),
                ["description"] = item.Description.Trim(),
                ["image"] = item.Image.Trim(),
                ["notes"] = item.Notes.Trim(),
                ["type"] = string.IsNullOrWhiteSpace(item.Type) ? "custom" : item.Type,
                ["createdAt"] = now,
                ["updatedAt"] = now
            };

            var created = _firebaseClient
                .PostAsync<FirebasePushResponse>("customItems", payload)
                .GetAwaiter()
                .GetResult();

            if (created == null || string.IsNullOrWhiteSpace(created.Name))
            {
                throw new InvalidOperationException("Firebase did not return a custom item key.");
            }

            item.Id = created.Name;
            item.Type = "custom";
            return item;
        }

        private sealed class FirebaseCustomItemRecord
        {
            public string? Description { get; set; }
            public string? Image { get; set; }
            public string? Name { get; set; }
            public string? Notes { get; set; }
            public string? Type { get; set; }
        }

        private sealed class FirebasePushResponse
        {
            public string? Name { get; set; }
        }
    }
}
