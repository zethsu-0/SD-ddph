using System.IO;
using System.Windows.Media.Imaging;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace ddph.Receipts;

public static class ReceiptPdfService
{
    public static ReceiptPdfResult Generate(
        string imagePath,
        IReadOnlyList<CartItem> items,
        decimal subtotal,
        decimal discount,
        decimal total,
        decimal vatableSales,
        decimal vatAmount,
        decimal payment,
        decimal change,
        string discountLabel,
        string reference,
        DateTime createdAt)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = new ReceiptDocument(items, subtotal, discount, total, vatableSales, vatAmount, payment, change, discountLabel, reference, createdAt);
        var previewImages = document
            .GenerateImages(new ImageGenerationSettings
            {
                RasterDpi = 160
            })
            .ToList();

        var savedImages = SaveImages(imagePath, previewImages);

        return new ReceiptPdfResult(imagePath, savedImages);
    }

    private static IReadOnlyList<byte[]> SaveImages(string imagePath, IReadOnlyList<byte[]> images)
    {
        if (images.Count == 0)
        {
            return Array.Empty<byte[]>();
        }

        if (images.Count == 1)
        {
            File.WriteAllBytes(imagePath, images[0]);
            return images;
        }

        var combinedImage = CombineImages(images);
        File.WriteAllBytes(imagePath, combinedImage);
        return new[] { combinedImage };
    }

    private static byte[] CombineImages(IReadOnlyList<byte[]> images)
    {
        var bitmaps = images.Select(LoadBitmap).ToList();
        var width = bitmaps.Max(bitmap => bitmap.PixelWidth);
        var height = bitmaps.Sum(bitmap => bitmap.PixelHeight);
        var stride = width * 4;
        var pixels = new byte[height * stride];
        var offsetY = 0;

        foreach (var bitmap in bitmaps)
        {
            var sourceStride = bitmap.PixelWidth * 4;
            var sourcePixels = new byte[bitmap.PixelHeight * sourceStride];
            bitmap.CopyPixels(sourcePixels, sourceStride, 0);

            for (var y = 0; y < bitmap.PixelHeight; y++)
            {
                Buffer.BlockCopy(sourcePixels, y * sourceStride, pixels, (offsetY + y) * stride, sourceStride);
            }

            offsetY += bitmap.PixelHeight;
        }

        var combinedBitmap = BitmapSource.Create(
            width,
            height,
            96,
            96,
            System.Windows.Media.PixelFormats.Bgra32,
            null,
            pixels,
            stride);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(combinedBitmap));

        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static BitmapSource LoadBitmap(byte[] imageBytes)
    {
        using var stream = new MemoryStream(imageBytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return new FormatConvertedBitmap(image, System.Windows.Media.PixelFormats.Bgra32, null, 0);
    }
}

public sealed record ReceiptPdfResult(string FilePath, IReadOnlyList<byte[]> PreviewImages);
