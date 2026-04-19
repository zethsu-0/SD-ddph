# Product and category fetch flow

This note shows actual fetch logic.

## Main trigger

When view model starts, it loads products.

```csharp
public MainWindowViewModel()
{
    Products = new ObservableCollection<Product>();
    Categories = new ObservableCollection<string> { "All" };
    CartItems = new ObservableCollection<CartItem>();
    FilteredProducts = CollectionViewSource.GetDefaultView(Products);
    FilteredProducts.Filter = FilterProducts;
    RefreshProductsCommand = new RelayCommand(_ => LoadProducts());

    LoadProducts();
}
```

## Product fetch call

This is main loading method.

```csharp
private void LoadProducts()
{
    try
    {
        Products.Clear();

        foreach (var product in _productRepository.GetProducts())
        {
            Products.Add(product);
        }

        RebuildCategories();
        RefreshFilters();
        OnPropertyChanged(nameof(ProductCount));
    }
    catch (System.Exception ex)
    {
        MessageBox.Show(
            $"Unable to load products into the main screen.\n\n{ex.Message}",
            "Database Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
```

## Repository fetch logic

This method reads product data from Firebase.

```csharp
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
```

This means app reads:

```text
products.json
```

from Firebase Realtime Database.

## Firebase connection logic

This class handles REST requests.

```csharp
public class FirebaseDatabaseClient
{
    private const string DatabaseUrl = "https://dreamdoughph-88e46-default-rtdb.asia-southeast1.firebasedatabase.app";

    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri($"{DatabaseUrl.TrimEnd('/')}/")
    };

    public async Task<T?> GetAsync<T>(string path)
    {
        using var response = await HttpClient.GetAsync(ToFirebasePath(path)).ConfigureAwait(false);
        await EnsureSuccess(response, path).ConfigureAwait(false);

        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json) || json == "null")
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static string ToFirebasePath(string path)
    {
        return $"{path.TrimStart('/').TrimEnd('/')}.json";
    }
}
```

If path is `products`, request becomes:

```text
https://dreamdoughph-88e46-default-rtdb.asia-southeast1.firebasedatabase.app/products.json
```

## Product mapping logic

Firebase records are converted into app `Product`.

```csharp
private static Product MapToProduct(string key, FirebaseProductRecord record)
{
    return new Product
    {
        Id = key,
        Description = record.Description ?? string.Empty,
        ProductName = record.Name ?? string.Empty,
        ImageUrl = record.Image ?? string.Empty,
        Category = NormalizeCategory(record.Category),
        Price = record.Price
    };
}

private static string NormalizeCategory(string? category)
{
    return string.IsNullOrWhiteSpace(category) ? "Uncategorized" : category.Trim();
}
```

Important behavior:

- Firebase key becomes product `Id`
- blank category becomes `Uncategorized`
- product fields come from Firebase record

## Category loading logic

Categories are not fetched from `categories.json` for main screen.

They are built from loaded products.

```csharp
private void RebuildCategories()
{
    var categories = Products
        .Select(product => string.IsNullOrWhiteSpace(product.Category) ? "Uncategorized" : product.Category)
        .Distinct(System.StringComparer.OrdinalIgnoreCase)
        .OrderBy(category => category, System.StringComparer.OrdinalIgnoreCase)
        .ToList();

    Categories.Clear();
    Categories.Add("All");

    foreach (var category in categories)
    {
        Categories.Add(category);
    }

    if (!Categories.Contains(SelectedCategory))
    {
        _selectedCategory = "All";
        OnPropertyChanged(nameof(SelectedCategory));
    }
}
```

So category filter comes from `Products` collection after fetch.

## Why `categories` exists in Firebase

`categories` node is maintained when product is added or updated.

It is not current source for category filter display.

```csharp
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
```

## Short version

Products are fetched from Firebase `products.json`.

Categories shown in UI are built from fetched products.

`categories` node is only checked or created during save/update flow.
