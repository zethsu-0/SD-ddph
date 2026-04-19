using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ddphkiosk;

public sealed class FirebaseKioskService
{
    private const string BaseUrl = "https://dreamdoughph-88e46-default-rtdb.asia-southeast1.firebasedatabase.app";
    private const string PlaceholderImage = "placeholder";

    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri(BaseUrl)
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<MenuData> GetMenuAsync()
    {
        var productsResponse = await HttpClient.GetFromJsonAsync<FirebaseProductsResponse>("/products.json", JsonOptions);
        var products = BuildProducts(productsResponse ?? []);
        var categories = BuildCategories(products);

        return new MenuData
        {
            Products = products,
            Categories = categories
        };
    }

    public async Task<string> CreateOrderAsync(OrderCreateRequest request)
    {
        var response = await HttpClient.PostAsJsonAsync("/kioskSales.json", request, JsonOptions);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<FirebaseCreateResponse>(JsonOptions);
        if (string.IsNullOrWhiteSpace(result?.Name))
        {
            throw new InvalidOperationException("Firebase did not return order id.");
        }

        return result.Name;
    }

    private static List<ProductCard> BuildProducts(FirebaseProductsResponse productsResponse)
    {
        var products = new List<ProductCard>();

        foreach (var pair in productsResponse.OrderBy(item => item.Value?.Name ?? item.Key))
        {
            var dto = pair.Value;
            if (dto is null || string.IsNullOrWhiteSpace(dto.Name))
            {
                continue;
            }

            var category = string.IsNullOrWhiteSpace(dto.Category) ? "Uncategorized" : dto.Category.Trim();
            var name = dto.Name.Trim();
            products.Add(new ProductCard
            {
                Id = pair.Key,
                Name = name,
                ShortName = Shorten(name),
                Category = category,
                Eyebrow = category.ToUpperInvariant(),
                Description = BuildDescription(category, dto.Image),
                Badge = BuildBadge(name),
                Image = string.IsNullOrWhiteSpace(dto.Image) ? PlaceholderImage : dto.Image!,
                ProductImageSource = CreateImageSource(dto.Image),
                Price = dto.Price,
                ArtBrush = CreateBrush(name)
            });
        }

        return products;
    }

    private static List<CategoryTab> BuildCategories(List<ProductCard> products)
    {
        var categoryNames = products
            .Select(item => string.IsNullOrWhiteSpace(item.Category) ? "Uncategorized" : item.Category.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var categories = new List<CategoryTab>
        {
            new CategoryTab
            {
                Key = "all",
                Name = "All"
            }
        };

        categories.AddRange(categoryNames.Select(name => new CategoryTab
        {
            Key = name.ToLowerInvariant().Replace(" ", "-"),
            Name = name
        }));

        return categories;
    }

    private static string Shorten(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= 2)
        {
            return name;
        }

        return string.Join(' ', words.Take(2));
    }

    private static string BuildDescription(string category, string? image)
    {
        var imageState = string.IsNullOrWhiteSpace(image) ? "Placeholder art." : "Live catalog image.";
        return $"{category} from Firebase menu. {imageState}";
    }

    private static string BuildBadge(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return "DD";
        }

        return string.Concat(words.Take(2).Select(word => char.ToUpperInvariant(word[0])));
    }

    private static Brush CreateBrush(string name)
    {
        var palette = new[]
        {
            "#B1704C",
            "#748F5A",
            "#8270A3",
            "#B69164",
            "#D1A04A",
            "#8D4E36"
        };

        var index = Math.Abs(name.GetHashCode()) % palette.Length;
        return (Brush)new BrushConverter().ConvertFromString(palette[index])!;
    }

    private static BitmapImage? CreateImageSource(string? image)
    {
        if (string.IsNullOrWhiteSpace(image) || image == PlaceholderImage)
        {
            return null;
        }

        if (!Uri.TryCreate(image, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return new BitmapImage(uri);
    }
}

public sealed class FirebaseCreateResponse
{
    public string? Name { get; set; }
}

public sealed class MenuData
{
    public List<ProductCard> Products { get; set; } = [];

    public List<CategoryTab> Categories { get; set; } = [];
}
