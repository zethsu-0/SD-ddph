using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ddph.Models;

namespace ddph.Data
{
    public class OrderRepository
    {
        private readonly FirebaseDatabaseClient _firebaseClient = new();

        public async Task<List<OnlineOrder>> GetOnlineOrdersAsync()
        {
            var orders = _firebaseClient
                .GetAsync<Dictionary<string, FirebaseOrderRecord>>("orders")
                .ConfigureAwait(false);

            var orderRecords = await orders;

            if (orderRecords == null)
            {
                return new List<OnlineOrder>();
            }

            return orderRecords
                .Where(entry => entry.Value != null)
                .Select(entry => MapOrder(entry.Key, entry.Value!))
                .OrderByDescending(order => order.Date)
                .ToList();
        }

        public async Task<List<OnlineOrder>> GetRegisterOrdersAsync()
        {
            var orders = _firebaseClient
                .GetAsync<Dictionary<string, JsonElement>>("walk-in-orders")
                .ConfigureAwait(false);

            var orderRecords = await orders;

            if (orderRecords == null)
            {
                return new List<OnlineOrder>();
            }

            return orderRecords
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

        public async Task<List<OnlineOrder>> GetKioskSalesAsync()
        {
            var sales = _firebaseClient
                .GetAsync<Dictionary<string, JsonElement>>("kioskSales")
                .ConfigureAwait(false);

            var saleRecords = await sales;

            if (saleRecords == null)
            {
                return new List<OnlineOrder>();
            }

            return saleRecords
                .Where(entry => !entry.Key.StartsWith("_") && entry.Value.ValueKind == JsonValueKind.Object)
                .Select(entry => new
                {
                    entry.Key,
                    Record = entry.Value.Deserialize<FirebaseKioskSaleRecord>(new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    })
                })
                .Where(entry => entry.Record != null)
                .Select(entry => MapKioskSale(entry.Key, entry.Record!))
                .OrderByDescending(order => order.Date)
                .ToList();
        }

        public async Task<OnlineOrder?> GetOrderByReferenceAsync(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return null;
            }

            var orderId = reference.Trim();
            var exactOrder = await GetExactOrderByReferenceAsync(orderId).ConfigureAwait(false);
            if (exactOrder != null)
            {
                return exactOrder;
            }

            return await FindPartialOrderByReferenceAsync(orderId).ConfigureAwait(false);
        }

        private async Task<OnlineOrder?> GetExactOrderByReferenceAsync(string orderId)
        {
            var onlineOrder = await _firebaseClient
                .GetAsync<FirebaseOrderRecord>($"orders/{orderId}")
                .ConfigureAwait(false);

            if (onlineOrder != null)
            {
                return MapOrder(orderId, onlineOrder);
            }

            var kioskSale = await _firebaseClient
                .GetAsync<FirebaseKioskSaleRecord>($"kioskSales/{orderId}")
                .ConfigureAwait(false);

            if (kioskSale != null)
            {
                return MapKioskSale(orderId, kioskSale);
            }

            var walkInOrder = await _firebaseClient
                .GetAsync<FirebaseWalkInOrderRecord>($"walk-in-orders/{orderId}")
                .ConfigureAwait(false);

            if (walkInOrder != null)
            {
                return MapWalkInOrder(orderId, walkInOrder);
            }

            var customOrder = await _firebaseClient
                .GetAsync<FirebaseOrderRecord>($"customOrders/{orderId}")
                .ConfigureAwait(false);

            return customOrder == null ? null : MapOrder(orderId, customOrder, "Custom");
        }

        private async Task<OnlineOrder?> FindPartialOrderByReferenceAsync(string partialOrderId)
        {
            var normalizedPartial = NormalizeOrderId(partialOrderId);
            if (string.IsNullOrWhiteSpace(normalizedPartial))
            {
                return null;
            }

            var onlineOrders = await GetMatchingOrdersAsync<FirebaseOrderRecord>(
                    "orders",
                    normalizedPartial,
                    (id, record) => MapOrder(id, record))
                .ConfigureAwait(false);
            if (onlineOrders.Count > 0)
            {
                return onlineOrders[0];
            }

            var kioskSales = await GetMatchingOrdersAsync<FirebaseKioskSaleRecord>(
                    "kioskSales",
                    normalizedPartial,
                    (id, record) => MapKioskSale(id, record))
                .ConfigureAwait(false);
            if (kioskSales.Count > 0)
            {
                return kioskSales[0];
            }

            var walkInOrders = await GetMatchingOrdersAsync<FirebaseWalkInOrderRecord>(
                    "walk-in-orders",
                    normalizedPartial,
                    (id, record) => MapWalkInOrder(id, record))
                .ConfigureAwait(false);
            if (walkInOrders.Count > 0)
            {
                return walkInOrders[0];
            }

            var customOrders = await GetMatchingOrdersAsync<FirebaseOrderRecord>(
                    "customOrders",
                    normalizedPartial,
                    (id, record) => MapOrder(id, record, "Custom"))
                .ConfigureAwait(false);

            return customOrders.FirstOrDefault();
        }

        private async Task<List<OnlineOrder>> GetMatchingOrdersAsync<TRecord>(
            string node,
            string normalizedPartialOrderId,
            System.Func<string, TRecord, OnlineOrder> mapOrder)
        {
            var records = await _firebaseClient
                .GetAsync<Dictionary<string, JsonElement>>(node)
                .ConfigureAwait(false);

            if (records == null)
            {
                return new List<OnlineOrder>();
            }

            return records
                .Where(entry => !entry.Key.StartsWith("_") &&
                    entry.Value.ValueKind == JsonValueKind.Object &&
                    NormalizeOrderId(entry.Key).Contains(normalizedPartialOrderId))
                .Select(entry => new FirebaseOrderMatch<TRecord>(
                    entry.Key,
                    entry.Value.Deserialize<TRecord>(new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    })))
                .Where(entry => entry.Record != null)
                .Select(entry => mapOrder(entry.Id, entry.Record!))
                .OrderByDescending(order => order.Date)
                .ToList();
        }

        private static string NormalizeOrderId(string orderId)
        {
            return orderId.Trim().TrimStart('-').ToUpperInvariant();
        }

        private sealed record FirebaseOrderMatch<TRecord>(string Id, TRecord? Record);

        public async Task UpdateOrderStatusAsync(string orderId, string status, string orderNode = "orders")
        {
            await _firebaseClient
                .PatchAsync(
                    $"{orderNode}/{orderId}",
                    new Dictionary<string, object?>
                    {
                        ["status"] = status,
                        ["updatedAt"] = System.DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
                    })
                .ConfigureAwait(false);
        }

        public async Task AddCustomOrderAsync(CustomOrderSubmission submission)
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

            await _firebaseClient
                .PostAsync<object>("orders", orderPayload)
                .ConfigureAwait(false);
        }

        private static OnlineOrder MapOrder(string id, FirebaseOrderRecord record, string orderSource = "Online")
        {
            var order = new OnlineOrder
            {
                Id = id,
                CustomerName = record.CustomerName ?? string.Empty,
                CustomerPhone = record.CustomerPhone ?? string.Empty,
                CustomerEmail = record.CustomerEmail ?? string.Empty,
                OrderSource = orderSource,
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
                Status = record.Status ?? "pending",
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

        private static OnlineOrder MapKioskSale(string id, FirebaseKioskSaleRecord record)
        {
            var order = new OnlineOrder
            {
                Id = id,
                CustomerName = string.IsNullOrWhiteSpace(record.CustomerName) ? "Kiosk Customer" : record.CustomerName!,
                CustomerPhone = record.CustomerPhone ?? string.Empty,
                CustomerEmail = string.Empty,
                OrderSource = "Kiosk",
                OrderType = record.OrderType ?? "kiosk",
                Status = record.Status ?? "pending",
                PaymentStatus = record.PaymentStatus ?? "unpaid",
                PickupDate = record.PickupDate ?? string.Empty,
                PickupTime = record.PickupTime ?? string.Empty,
                Notes = record.Notes ?? string.Empty,
                Subtotal = record.Subtotal,
                Total = record.Total,
                Date = string.IsNullOrWhiteSpace(record.Date) ? FormatDate(record.CreatedAt) : record.Date!
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

        private sealed class FirebaseKioskSaleRecord
        {
            public string? CreatedAt { get; set; }
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
