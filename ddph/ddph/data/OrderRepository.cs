using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using ddph.Models;

namespace ddph.Data
{
    public class OrderRepository
    {
        private readonly FirebaseDatabaseClient _firebaseClient = new();

        public List<OnlineOrder> GetOnlineOrders()
        {
            var orders = _firebaseClient
                .GetAsync<Dictionary<string, FirebaseOrderRecord>>("orders")
                .GetAwaiter()
                .GetResult();

            if (orders == null)
            {
                return new List<OnlineOrder>();
            }

            return orders
                .Where(entry => entry.Value != null)
                .Select(entry => MapOrder(entry.Key, entry.Value!))
                .OrderByDescending(order => order.Date)
                .ToList();
        }

        public List<OnlineOrder> GetRegisterOrders()
        {
            var orders = _firebaseClient
                .GetAsync<Dictionary<string, JsonElement>>("walk-in-orders")
                .GetAwaiter()
                .GetResult();

            if (orders == null)
            {
                return new List<OnlineOrder>();
            }

            return orders
                .Where(entry => !entry.Key.StartsWith("_") && entry.Value.ValueKind == JsonValueKind.Object)
                .Select(entry => new
                {
                    entry.Key,
                    Record = entry.Value.Deserialize<FirebaseWalkInOrderRecord>(new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    })
                })
                .Where(entry => entry.Record != null)
                .Select(entry => MapWalkInOrder(entry.Key, entry.Record!))
                .OrderByDescending(order => order.Date)
                .ToList();
        }

        public void UpdateOrderStatus(string orderId, string status, bool isRegisterOrder = false)
        {
            _firebaseClient
                .PatchAsync(
                    $"{(isRegisterOrder ? "walk-in-orders" : "orders")}/{orderId}",
                    new Dictionary<string, object?>
                    {
                        ["status"] = status,
                        ["updatedAt"] = System.DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
                    })
                .GetAwaiter()
                .GetResult();
        }

        public void AddCustomOrder(CustomOrderSubmission submission)
        {
            var itemPayload = new Dictionary<string, object?>
            {
                ["name"] = $"Custom {submission.ProductType}",
                ["category"] = submission.ProductType.ToLowerInvariant(),
                ["quantity"] = submission.Quantity,
                ["price"] = 0,
                ["size"] = submission.Size,
                ["flavor"] = string.IsNullOrWhiteSpace(submission.Flavor) ? "To be determined" : submission.Flavor,
                ["designDescription"] = submission.DesignDescription
            };

            var orderPayload = new Dictionary<string, object?>
            {
                ["customerName"] = submission.CustomerName,
                ["customerPhone"] = submission.CustomerPhone,
                ["customerEmail"] = submission.CustomerEmail,
                ["customerAddress"] = submission.DeliveryAddress,
                ["pickupDate"] = submission.PickupDate,
                ["pickupTime"] = submission.PickupTime,
                ["items"] = new[] { itemPayload },
                ["notes"] = submission.AdditionalNotes,
                ["subtotal"] = 0,
                ["total"] = 0,
                ["status"] = "pending",
                ["orderType"] = "custom",
                ["paymentStatus"] = "unpaid",
                ["referenceImageUrl"] = submission.ReferenceImageUrl,
                ["createdAt"] = System.DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                ["date"] = System.DateTime.Now.ToString("MMM dd, yyyy, hh:mm tt", CultureInfo.InvariantCulture)
            };

            _firebaseClient
                .PostAsync<object>("orders", orderPayload)
                .GetAwaiter()
                .GetResult();
        }

        private static OnlineOrder MapOrder(string id, FirebaseOrderRecord record)
        {
            var order = new OnlineOrder
            {
                Id = id,
                CustomerName = record.CustomerName ?? string.Empty,
                CustomerPhone = record.CustomerPhone ?? string.Empty,
                CustomerEmail = record.CustomerEmail ?? string.Empty,
                OrderSource = "Online",
                OrderType = record.OrderType ?? "standard",
                Status = record.Status ?? "pending",
                PaymentStatus = record.PaymentStatus ?? "unpaid",
                PickupDate = record.PickupDate ?? string.Empty,
                PickupTime = record.PickupTime ?? string.Empty,
                Notes = record.Notes ?? string.Empty,
                Subtotal = record.Subtotal,
                Total = record.Total,
                Date = record.Date ?? string.Empty
            };

            if (record.Items != null)
            {
                foreach (var item in record.Items)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    order.Items.Add(new OnlineOrderItem
                    {
                        Name = item.Name ?? string.Empty,
                        Category = item.Category ?? string.Empty,
                        Quantity = item.Quantity,
                        Price = item.Price
                    });
                }
            }

            return order;
        }

        private static OnlineOrder MapWalkInOrder(string id, FirebaseWalkInOrderRecord record)
        {
            var order = new OnlineOrder
            {
                Id = id,
                CustomerName = string.IsNullOrWhiteSpace(record.CustomerName) ? "Walk-in Customer" : record.CustomerName!,
                CustomerPhone = string.Empty,
                CustomerEmail = string.IsNullOrWhiteSpace(record.CashierName) ? string.Empty : $"Cashier: {record.CashierName}",
                OrderSource = "Register",
                Status = record.Status ?? "completed",
                PaymentStatus = record.PaymentStatus ?? "paid",
                PickupDate = string.Empty,
                PickupTime = string.Empty,
                Notes = record.Notes ?? string.Empty,
                Subtotal = record.Subtotal,
                Total = record.Total,
                Payment = record.Payment,
                Change = record.Change,
                Date = FormatDate(record.CreatedAt)
            };

            if (record.Items != null)
            {
                foreach (var item in record.Items)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    order.Items.Add(new OnlineOrderItem
                    {
                        Name = item.Name ?? string.Empty,
                        Category = item.Category ?? string.Empty,
                        Quantity = item.Quantity,
                        Price = item.Price
                    });
                }
            }

            return order;
        }

        private static string FormatDate(string? createdAt)
        {
            if (string.IsNullOrWhiteSpace(createdAt))
            {
                return string.Empty;
            }

            if (System.DateTime.TryParse(
                createdAt,
                CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var parsed))
            {
                return parsed.ToLocalTime().ToString("MMM dd, yyyy, hh:mm tt", CultureInfo.InvariantCulture);
            }

            return createdAt;
        }

        private sealed class FirebaseOrderRecord
        {
            public string? CustomerEmail { get; set; }
            public string? CustomerName { get; set; }
            public string? CustomerPhone { get; set; }
            public string? Date { get; set; }
            public List<FirebaseOrderItemRecord?>? Items { get; set; }
            public string? Notes { get; set; }
            public string? OrderType { get; set; }
            public string? PaymentStatus { get; set; }
            public string? PickupDate { get; set; }
            public string? PickupTime { get; set; }
            public string? Status { get; set; }
            public decimal Subtotal { get; set; }
            public decimal Total { get; set; }
        }

        private sealed class FirebaseOrderItemRecord
        {
            public string? Category { get; set; }
            public string? Name { get; set; }
            public decimal Price { get; set; }
            public int Quantity { get; set; }
        }

        private sealed class FirebaseWalkInOrderRecord
        {
            public string? CashierName { get; set; }
            public decimal Change { get; set; }
            public string? CreatedAt { get; set; }
            public string? CustomerName { get; set; }
            public List<FirebaseOrderItemRecord?>? Items { get; set; }
            public string? Notes { get; set; }
            public decimal Payment { get; set; }
            public string? PaymentStatus { get; set; }
            public string? Status { get; set; }
            public decimal Subtotal { get; set; }
            public decimal Total { get; set; }
        }

        public sealed class CustomOrderSubmission
        {
            public string AdditionalNotes { get; set; } = string.Empty;
            public string CustomerEmail { get; set; } = string.Empty;
            public string CustomerName { get; set; } = string.Empty;
            public string CustomerPhone { get; set; } = string.Empty;
            public string DeliveryAddress { get; set; } = string.Empty;
            public string DesignDescription { get; set; } = string.Empty;
            public string Flavor { get; set; } = string.Empty;
            public string PickupDate { get; set; } = string.Empty;
            public string PickupTime { get; set; } = string.Empty;
            public string ProductType { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public string ReferenceImageUrl { get; set; } = string.Empty;
            public string Size { get; set; } = string.Empty;
        }
    }
}
