using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ddphkiosk;

public sealed class FirebaseKioskService
{
    private const string BaseUrl = "https://dreamdoughph-88e46-default-rtdb.asia-southeast1.firebasedatabase.app";
    private const string StorageBucket = "dreamdoughph-88e46.appspot.com";
    private const string PlaceholderImage = "placeholder";
    private static readonly SemaphoreSlim ImageLoadGate = new(6);

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

    public async Task<BitmapImage?> GetProductImageAsync(ProductCard product)
    {
        if (string.IsNullOrWhiteSpace(product.Image) || product.Image == PlaceholderImage)
        {
            return null;
        }

        await ImageLoadGate.WaitAsync();
        try
        {
            return await CreateImageSourceAsync(product.Image);
        }
        finally
        {
            ImageLoadGate.Release();
        }
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

    private static async Task<BitmapImage?> CreateImageSourceAsync(string? image)
    {
        if (string.IsNullOrWhiteSpace(image) || image == PlaceholderImage)
        {
            return null;
        }

        if (TryCreateDataUriBitmap(image, out var dataBitmap))
        {
            return dataBitmap;
        }

        if (Uri.TryCreate(image, UriKind.Absolute, out var absoluteUri))
        {
            if (absoluteUri.IsFile && !File.Exists(absoluteUri.LocalPath))
            {
                return null;
            }

            if (string.Equals(absoluteUri.Scheme, "gs", StringComparison.OrdinalIgnoreCase))
            {
                if (TryCreateFirebaseStorageDownloadUri(absoluteUri, out var storageUri))
                {
                    return await CreateRemoteBitmapImageAsync(storageUri);
                }

                return null;
            }

            if (string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return await CreateRemoteBitmapImageAsync(absoluteUri);
            }

            if (!absoluteUri.IsFile)
            {
                return null;
            }

            return CreateBitmapImage(absoluteUri);
        }

        if (Path.IsPathRooted(image) && File.Exists(image))
        {
            return CreateBitmapImage(new Uri(image, UriKind.Absolute));
        }

        if (TryCreateFirebaseStorageDownloadUri(image, out var fallbackStorageUri))
        {
            return await CreateRemoteBitmapImageAsync(fallbackStorageUri);
        }

        {
            return null;
        }
    }

    private static bool TryCreateDataUriBitmap(string value, out BitmapImage? bitmap)
    {
        bitmap = null;

        if (!value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var commaIndex = value.IndexOf(',');
        if (commaIndex < 0)
        {
            return false;
        }

        var header = value[..commaIndex];
        if (!header.Contains(";base64", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var base64 = value[(commaIndex + 1)..];
        try
        {
            var bytes = Convert.FromBase64String(base64);
            using var stream = new MemoryStream(bytes, writable: false);

            var result = new BitmapImage();
            result.BeginInit();
            result.CacheOption = BitmapCacheOption.OnLoad;
            result.StreamSource = stream;
            result.EndInit();
            result.Freeze();

            bitmap = result;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCreateFirebaseStorageDownloadUri(string objectPath, out Uri uri)
    {
        uri = null!;

        var trimmed = objectPath.Trim();
        trimmed = trimmed.TrimStart('/');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (trimmed.StartsWith("gs://", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.TryCreate(trimmed, UriKind.Absolute, out var gsUri) &&
                   TryCreateFirebaseStorageDownloadUri(gsUri, out uri);
        }

        var encodedObject = Uri.EscapeDataString(trimmed.Replace('\\', '/'));
        var url = $"https://firebasestorage.googleapis.com/v0/b/{StorageBucket}/o/{encodedObject}?alt=media";
        if (Uri.TryCreate(url, UriKind.Absolute, out var downloadUri))
        {
            uri = downloadUri;
            return true;
        }

        return false;
    }

    private static bool TryCreateFirebaseStorageDownloadUri(Uri gsUri, out Uri uri)
    {
        uri = null!;

        if (!string.Equals(gsUri.Scheme, "gs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(gsUri.Host))
        {
            return false;
        }

        var bucket = gsUri.Host.Trim();
        if (!bucket.EndsWith(".appspot.com", StringComparison.OrdinalIgnoreCase))
        {
            bucket = StorageBucket;
        }

        var path = gsUri.AbsolutePath.Trim('/');
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var encodedObject = Uri.EscapeDataString(path);
        var url = $"https://firebasestorage.googleapis.com/v0/b/{bucket}/o/{encodedObject}?alt=media";
        if (Uri.TryCreate(url, UriKind.Absolute, out var downloadUri))
        {
            uri = downloadUri;
            return true;
        }

        return false;
    }

    private static BitmapImage? CreateBitmapImage(Uri uri)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = uri;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<BitmapImage?> CreateRemoteBitmapImageAsync(Uri uri)
    {
        try
        {
            var bytes = await HttpClient.GetByteArrayAsync(uri);
            using var stream = new MemoryStream(bytes, writable: false);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
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
