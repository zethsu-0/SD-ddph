using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ddphkiosk;

public static class ReceiptGenerator
{
    static ReceiptGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static IReadOnlyList<BitmapImage> CreatePreviewPages(OrderCreateRequest request)
    {
        var qrCodeBytes = CreateQrCodeBytes(request);
        var document = CreateDocument(request, qrCodeBytes);
        var images = document.GenerateImages(new ImageGenerationSettings
        {
            RasterDpi = 160
        });

        return images.Select(CreateBitmapImage).ToList();
    }

    private static IDocument CreateDocument(OrderCreateRequest request, byte[] qrCodeBytes)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(28);
                page.DefaultTextStyle(style => style.FontFamily("Lato").FontSize(10).FontColor(Colors.Grey.Darken4));

                page.Content().Column(column =>
                {
                    column.Spacing(9);
                    column.Item().AlignCenter().Text("Dream Dough PH").FontSize(22).Bold().FontColor(Colors.Brown.Darken2);
                    column.Item().AlignCenter().Text("Order Receipt").FontSize(11).FontColor(Colors.Grey.Darken2);
                    column.Item().AlignCenter().Text($"Order #{request.OrderNumber.ToString("D3", CultureInfo.InvariantCulture)}")
                        .FontSize(18).Bold().FontColor(Colors.Brown.Darken2);
                    column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(details =>
                        {
                            details.Spacing(4);
                            AddDetail(details, "Customer", request.CustomerName);
                            AddDetail(details, "Phone", request.CustomerPhone);
                            AddDetail(details, "Pickup", $"{request.PickupDate} {request.PickupTime}");
                            AddDetail(details, "Date", request.Date);
                        });

                        row.ConstantItem(92).PaddingLeft(12).Image(qrCodeBytes).FitWidth();
                    });

                    column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.ConstantColumn(34);
                            columns.ConstantColumn(58);
                            columns.ConstantColumn(66);
                        });

                        AddHeaderCell(table, "Item");
                        AddHeaderCell(table, "Qty");
                        AddHeaderCell(table, "Price");
                        AddHeaderCell(table, "Total");

                        foreach (var item in request.Items)
                        {
                            AddBodyCell(table, item.Name);
                            AddBodyCell(table, item.Quantity.ToString(CultureInfo.InvariantCulture));
                            AddBodyCell(table, item.Price.ToPeso());
                            AddBodyCell(table, item.Subtotal.ToPeso());
                        }
                    });

                    column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    column.Item().AlignRight().Column(totals =>
                    {
                        totals.Spacing(4);
                        totals.Item().Text($"Subtotal: {request.Subtotal.ToPeso()}").SemiBold();
                        totals.Item().Text($"Total: {request.Total.ToPeso()}").FontSize(15).Bold().FontColor(Colors.Brown.Darken2);
                    });

                    if (!string.IsNullOrWhiteSpace(request.Notes))
                    {
                        column.Item().Background(Colors.Grey.Lighten4).Padding(8).Column(notes =>
                        {
                            notes.Spacing(3);
                            notes.Item().Text("Notes").SemiBold();
                            notes.Item().Text(request.Notes);
                        });
                    }

                    column.Item().PaddingTop(4).AlignCenter().Text("Please confirm this receipt before sending the order.")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            });
        });
    }

    private static void AddDetail(ColumnDescriptor column, string label, string value)
    {
        column.Item().Text(text =>
        {
            text.Span($"{label}: ").SemiBold();
            text.Span(value);
        });
    }

    private static void AddHeaderCell(TableDescriptor table, string text)
    {
        table.Cell().Background(Colors.Grey.Lighten3).PaddingVertical(5).PaddingHorizontal(4).Text(text).SemiBold();
    }

    private static void AddBodyCell(TableDescriptor table, string text)
    {
        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(5).PaddingHorizontal(4).Text(text);
    }

    private static byte[] CreateQrCodeBytes(OrderCreateRequest request)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(CreateQrPayload(request), QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(data);
        return qrCode.GetGraphic(10);
    }

    private static string CreateQrPayload(OrderCreateRequest request)
    {
        var items = string.Join(", ", request.Items.Select(item => $"{item.Quantity}x {item.Name}"));
        var builder = new StringBuilder();
        builder.AppendLine("Dream Dough PH");
        builder.AppendLine($"Order #: {request.OrderNumber.ToString("D3", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"Customer: {request.CustomerName}");
        builder.AppendLine($"Phone: {request.CustomerPhone}");
        builder.AppendLine($"Pickup: {request.PickupDate} {request.PickupTime}");
        builder.AppendLine($"Items: {items}");
        builder.AppendLine($"Total: {request.Total.ToPeso()}");
        builder.AppendLine($"Created: {request.CreatedAt}");
        return builder.ToString();
    }

    private static BitmapImage CreateBitmapImage(byte[] imageBytes)
    {
        using var stream = new MemoryStream(imageBytes, writable: false);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
