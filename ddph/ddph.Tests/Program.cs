using ddph;
using ddph.Data;
using ddph.Models;
using ddph.ViewModels;

var viewModel = new MainWindowViewModel();
var order = CreateOrder("order-1", "product-1", 2);

viewModel.AddOrderToCart(order);
viewModel.AddOrderToCart(order);

AssertEqual(1, viewModel.CartItems.Count, "same order should have one cart line");
AssertEqual(2, viewModel.CartItems[0].Qty, "same order should not add quantity twice");

var secondOrder = CreateOrder("order-2", "product-1", 2);
viewModel.AddOrderToCart(secondOrder);

AssertEqual(1, viewModel.CartItems.Count, "same product should still share one cart line");
AssertEqual(2, viewModel.CartItems[0].Qty, "different order should replace current cart");

var firebase = new FakeFirebaseDatabaseClient();
var salesRepository = new SalesRepository(firebase);
var saleReference = await salesRepository.CheckoutSaleAsync(
    new List<CartItem>
    {
        new()
        {
            ProductId = "product-1",
            Item = "Glazed",
            Category = "Donut",
            Qty = 2,
            Price = 10m
        }
    },
    "cashier",
    20m,
    0m,
    null,
    "kiosk-1",
    "Maria Santos",
    "09171234567");

AssertEqual("kiosk-1", saleReference, "kiosk checkout should keep original transaction id");
AssertEqual("walk-in-orders/kiosk-1", firebase.PutPath, "kiosk checkout should save one walk-in sale");
AssertEqual("kioskSales/kiosk-1", firebase.DeletedPath, "kiosk checkout should delete original kiosk sale");
AssertEqual(string.Empty, firebase.PostPath, "kiosk checkout should not create extra sale");
AssertEqual("Maria Santos", firebase.PutPayloadValue("customerName"), "kiosk checkout should save customer name");
AssertEqual("09171234567", firebase.PutPayloadValue("customerPhone"), "kiosk checkout should save customer phone");

static OnlineOrder CreateOrder(string id, string productId, int quantity)
{
    var order = new OnlineOrder
    {
        Id = id
    };

    order.Items.Add(new OnlineOrderItem
    {
        ProductId = productId,
        Name = "Glazed",
        Category = "Donut",
        Quantity = quantity,
        Price = 10m
    });

    return order;
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}. Expected {expected}, got {actual}.");
    }
}

sealed class FakeFirebaseDatabaseClient : IFirebaseDatabaseClient
{
    public string PatchedPath { get; private set; } = string.Empty;
    public string PutPath { get; private set; } = string.Empty;
    public string DeletedPath { get; private set; } = string.Empty;
    public string PostPath { get; private set; } = string.Empty;
    public object? PutPayload { get; private set; }

    public Task<T?> GetAsync<T>(string path)
    {
        return Task.FromResult<T?>(default);
    }

    public Task<T?> PostAsync<T>(string path, object payload)
    {
        PostPath = path;
        return Task.FromResult<T?>(default);
    }

    public Task PutAsync(string path, object payload)
    {
        PutPath = path;
        PutPayload = payload;
        return Task.CompletedTask;
    }

    public Task PatchAsync(string path, object payload)
    {
        PatchedPath = path;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string path)
    {
        DeletedPath = path;
        return Task.CompletedTask;
    }

    public object? PutPayloadValue(string key)
    {
        return PutPayload is Dictionary<string, object?> values && values.TryGetValue(key, out var value)
            ? value
            : null;
    }
}
