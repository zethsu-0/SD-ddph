using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ddph.Data
{
    public class SalesRepository
    {
        private readonly FirebaseDatabaseClient _firebaseClient = new();

        public void CheckoutSale(List<CartItem> cartItems, string cashierName, decimal payment)
        {
            var totalAmount = cartItems.Sum(item => item.Price * item.Qty);
            var changeAmount = payment - totalAmount;
            var createdAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            var items = cartItems.Select(item => new Dictionary<string, object?>
            {
                ["productId"] = item.ProductId,
                ["name"] = item.Item,
                ["quantity"] = item.Qty,
                ["price"] = item.Price,
                ["subtotal"] = item.Price * item.Qty
            }).ToList();

            var salePayload = new Dictionary<string, object?>
            {
                ["cashierName"] = cashierName,
                ["saleType"] = "walk-in",
                ["items"] = items,
                ["subtotal"] = totalAmount,
                ["total"] = totalAmount,
                ["payment"] = payment,
                ["change"] = changeAmount,
                ["createdAt"] = createdAt,
                ["updatedAt"] = createdAt
            };

            _firebaseClient.PostAsync<object>("posSales", salePayload).GetAwaiter().GetResult();

        }

    }
}
