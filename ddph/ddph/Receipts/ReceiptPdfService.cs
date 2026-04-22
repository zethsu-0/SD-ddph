using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace ddph.Receipts;

public static class ReceiptPdfService
{
    public static ReceiptPdfResult Generate(
        string filePath,
        IReadOnlyList<CartItem> items,
        decimal subtotal,
        decimal discount,
        decimal total,
        decimal payment,
        decimal change,
        string discountLabel,
        string reference,
        DateTime createdAt)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = new ReceiptDocument(items, subtotal, discount, total, payment, change, discountLabel, reference, createdAt);
        document.GeneratePdf(filePath);

        var previewImages = document
            .GenerateImages(new ImageGenerationSettings
            {
                RasterDpi = 144
            })
            .ToList();

        return new ReceiptPdfResult(filePath, previewImages);
    }
}

public sealed record ReceiptPdfResult(string FilePath, IReadOnlyList<byte[]> PreviewImages);
