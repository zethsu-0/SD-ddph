using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ddph.Data
{
    public class SalesRepository
    {
        private readonly IFirebaseDatabaseClient _firebaseClient;

        public SalesRepository()
            : this(new FirebaseDatabaseClient())
        {
        }

        public SalesRepository(IFirebaseDatabaseClient firebaseClient)
        {
            _firebaseClient = firebaseClient;
        }

        public async Task<string> CheckoutSaleAsync(List<CartItem> cartItems, string cashierName, decimal payment, decimal discountRate, string? discountType, string? sourceKioskSaleId = null, string? customerName = null, string? customerPhone = null)
        {
            var subtotalAmount = cartItems.Sum(item => item.Price * item.Qty);
            var discountAmount = Math.Round(subtotalAmount * (discountRate / 100m), 2, MidpointRounding.AwayFromZero);
            var totalAmount = subtotalAmount - discountAmount;
            var changeAmount = payment - totalAmount;
            var createdAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            var items = cartItems.Select(item => new Dictionary<string, object?>
            {
                ["productId"] = item.ProductId,
                ["name"] = item.Item,
                ["category"] = string.IsNullOrWhiteSpace(item.Category) ? "Uncategorized" : item.Category.Trim(),
                ["quantity"] = item.Qty,
                ["price"] = item.Price,
                ["subtotal"] = item.Price * item.Qty
            }).ToList();

            var salePayload = new Dictionary<string, object?>
            {
                ["cashierName"] = cashierName,
                ["customerName"] = customerName,
                ["customerPhone"] = customerPhone,
                ["saleType"] = "walk-in",
                ["items"] = items,
                ["subtotal"] = subtotalAmount,
                ["discountRate"] = discountRate,
                ["discountType"] = discountType,
                ["discountAmount"] = discountAmount,
                ["total"] = totalAmount,
                ["status"] = string.IsNullOrWhiteSpace(sourceKioskSaleId) ? "pending" : "confirmed",
                ["paymentStatus"] = "paid",
                ["payment"] = payment,
                ["change"] = changeAmount,
                ["createdAt"] = createdAt,
                ["updatedAt"] = createdAt
            };

            if (!string.IsNullOrWhiteSpace(sourceKioskSaleId))
            {
                await _firebaseClient
                    .PutAsync($"walk-in-orders/{sourceKioskSaleId}", salePayload)
                    .ConfigureAwait(false);

                await _firebaseClient
                    .DeleteAsync($"kioskSales/{sourceKioskSaleId}")
                    .ConfigureAwait(false);

                return sourceKioskSaleId;
            }

            var created = await _firebaseClient
                .PostAsync<FirebasePushResponse>("walk-in-orders", salePayload)
                .ConfigureAwait(false);

            if (created == null || string.IsNullOrWhiteSpace(created.Name))
            {
                throw new InvalidOperationException("Firebase did not return a sale reference.");
            }

            return created.Name;
        }

        private sealed class FirebasePushResponse
        {
            public string? Name { get; set; }
        }
    }
}
