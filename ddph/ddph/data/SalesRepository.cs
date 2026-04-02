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
            foreach (var cartItem in cartItems)
            {
                EnsureEnoughStock(cartItem);
            }

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

            foreach (var cartItem in cartItems)
            {
                UpdateStock(cartItem);
            }
        }

        private void EnsureEnoughStock(CartItem cartItem)
        {
            var product = _firebaseClient
                .GetAsync<FirebaseProductRecord>($"products/{cartItem.ProductId}")
                .GetAwaiter()
                .GetResult();

            if (product == null)
            {
                throw new System.InvalidOperationException($"Product '{cartItem.Item}' was not found.");
            }

            if (!product.Stock.HasValue)
            {
                return;
            }

            if (product.Stock.Value < cartItem.Qty)
            {
                throw new System.InvalidOperationException(
                    $"Not enough stock for '{cartItem.Item}'. Available stock: {product.Stock.Value}.");
            }
        }

        private void UpdateStock(CartItem cartItem)
        {
            var product = _firebaseClient
                .GetAsync<FirebaseProductRecord>($"products/{cartItem.ProductId}")
                .GetAwaiter()
                .GetResult();

            if (product?.Stock == null)
            {
                return;
            }

            var updatedStock = product.Stock.Value - cartItem.Qty;
            _firebaseClient
                .PatchAsync(
                    $"products/{cartItem.ProductId}",
                    new Dictionary<string, object?>
                    {
                        ["stock"] = updatedStock,
                        ["updatedAt"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                    })
                .GetAwaiter()
                .GetResult();
        }

        private sealed class FirebaseProductRecord
        {
            public int? Stock { get; set; }
        }
    }
}
