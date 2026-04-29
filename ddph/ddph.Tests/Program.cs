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

var categoryFirebase = new FakeFirebaseDatabaseClient();
categoryFirebase.Categories["donuts"] = new Dictionary<string, object?>
{
    ["name"] = "Donuts",
    ["order"] = 999,
    ["protected"] = false
};
categoryFirebase.Products["product-1"] = new Dictionary<string, object?>
{
    ["name"] = "Glazed",
    ["category"] = "Donuts",
    ["price"] = 10m
};
categoryFirebase.Products["product-2"] = new Dictionary<string, object?>
{
    ["name"] = "Cheesecake",
    ["category"] = "Cakes",
    ["price"] = 120m
};

var categoryRepository = new CategoryRepository(categoryFirebase);
await categoryRepository.AddCategoryAsync("Pastries");
AssertEqual("Pastries", categoryFirebase.CategoryName("pastries"), "add category should save category name");

await categoryRepository.RenameCategoryAsync("Donuts", "Doughnuts");
AssertEqual(false, categoryFirebase.Categories.ContainsKey("donuts"), "rename category should remove old key");
AssertEqual("Doughnuts", categoryFirebase.CategoryName("doughnuts"), "rename category should save new name");
AssertEqual("Doughnuts", categoryFirebase.Products["product-1"]["category"], "rename category should update matching products");
AssertEqual("Cakes", categoryFirebase.Products["product-2"]["category"], "rename category should not update other products");

await AssertThrowsAsync<InvalidOperationException>(
    () => categoryRepository.DeleteCategoryAsync("Doughnuts"),
    "Category is used by products.",
    "used category delete should be blocked");

await categoryRepository.DeleteCategoryAsync("Pastries");
AssertEqual(false, categoryFirebase.Categories.ContainsKey("pastries"), "unused category delete should remove category");

var inventoryViewModel = new InventoryViewModel(loadProducts: false);
inventoryViewModel.Products.Add(new Product { ProductName = "B", Category = "Cupcakes", Price = 20m });
inventoryViewModel.Products.Add(new Product { ProductName = "A", Category = "Cakes", Price = 30m });
inventoryViewModel.SortProductsCommand.Execute("ProductName");
AssertEqual("A", inventoryViewModel.FilteredProducts.Cast<Product>().First().ProductName, "product name sort should sort ascending first");
inventoryViewModel.SortProductsCommand.Execute("ProductName");
AssertEqual("B", inventoryViewModel.FilteredProducts.Cast<Product>().First().ProductName, "same product name sort should toggle descending");
inventoryViewModel.SortProductsCommand.Execute("Price");
AssertEqual(20m, inventoryViewModel.FilteredProducts.Cast<Product>().First().Price, "price sort should sort ascending first");

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

static async Task AssertThrowsAsync<TException>(Func<Task> action, string expectedMessage, string message)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException ex)
    {
        AssertEqual(expectedMessage, ex.Message, message);
        return;
    }

    throw new InvalidOperationException($"{message}. Expected {typeof(TException).Name}.");
}

sealed class FakeFirebaseDatabaseClient : IFirebaseDatabaseClient
{
    public Dictionary<string, Dictionary<string, object?>> Categories { get; } = new();
    public Dictionary<string, Dictionary<string, object?>> Products { get; } = new();
    public string PatchedPath { get; private set; } = string.Empty;
    public string PutPath { get; private set; } = string.Empty;
    public string DeletedPath { get; private set; } = string.Empty;
    public string PostPath { get; private set; } = string.Empty;
    public object? PutPayload { get; private set; }

    public Task<T?> GetAsync<T>(string path)
    {
        if (path == "categories")
        {
            return Task.FromResult((T?)(object)Categories);
        }

        if (path.StartsWith("categories/"))
        {
            var key = path["categories/".Length..];
            return Task.FromResult(Categories.TryGetValue(key, out var category) ? (T?)(object)category : default);
        }

        if (path == "products")
        {
            return Task.FromResult((T?)(object)Products);
        }

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
        if (path.StartsWith("categories/") && payload is Dictionary<string, object?> category)
        {
            Categories[path["categories/".Length..]] = category;
        }

        if (path.StartsWith("products/") && payload is Dictionary<string, object?> product)
        {
            Products[path["products/".Length..]] = product;
        }

        return Task.CompletedTask;
    }

    public Task PatchAsync(string path, object payload)
    {
        PatchedPath = path;
        if (path.StartsWith("products/") &&
            payload is Dictionary<string, object?> updates &&
            Products.TryGetValue(path["products/".Length..], out var product))
        {
            foreach (var update in updates)
            {
                product[update.Key] = update.Value;
            }
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string path)
    {
        DeletedPath = path;
        if (path.StartsWith("categories/"))
        {
            Categories.Remove(path["categories/".Length..]);
        }

        return Task.CompletedTask;
    }

    public object? PutPayloadValue(string key)
    {
        return PutPayload is Dictionary<string, object?> values && values.TryGetValue(key, out var value)
            ? value
            : null;
    }

    public object? CategoryName(string key)
    {
        return Categories.TryGetValue(key, out var values) && values.TryGetValue("name", out var value)
            ? value
            : null;
    }
}
