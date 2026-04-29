using System.Globalization;
using System.IO;
using System.Text;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ddphkiosk;

public static class ReceiptGenerator
{
    private const decimal VatRate = 0.12m;
    private const float ReceiptPageWidth = 420f;
    private const float MinimumReceiptPageHeight = 595f;

    private static readonly string ReceiptsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Dream Dough PH",
        "Receipts");

    static ReceiptGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static IReadOnlyList<BitmapImage> CreatePreviewPages(OrderCreateRequest request)
    {
        return GenerateReceiptImages(request).Select(CreateBitmapImage).ToList();
    }

    public static async Task<string> SaveReceiptLocallyAsync(OrderCreateRequest request)
    {
        var imageBytes = GenerateReceiptImages(request).ToList();
        if (imageBytes.Count == 0)
        {
            throw new InvalidOperationException("Receipt did not generate any image pages.");
        }

        Directory.CreateDirectory(ReceiptsFolder);

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var fileName = $"receipt-{request.OrderNumber.ToString("D3", CultureInfo.InvariantCulture)}-{timestamp}.png";
        var filePath = Path.Combine(ReceiptsFolder, fileName);

        await File.WriteAllBytesAsync(filePath, imageBytes[0]);

        request.ReceiptImageUri = filePath;
        return filePath;
    }

    public static bool PrintReceiptImage(string receiptImagePath)
    {
        if (!File.Exists(receiptImagePath))
        {
            throw new FileNotFoundException("Receipt image was not found.", receiptImagePath);
        }

        var printDialog = new PrintDialog();

        if (printDialog.ShowDialog() != true)
        {
            return false;
        }

        ValidatePrintQueue(printDialog.PrintQueue);

        var pageSize = GetPrintablePageSize(printDialog, printDialog.PrintQueue);
        var document = CreatePrintableDocument([LoadReceiptImage(receiptImagePath)], pageSize);
        printDialog.PrintDocument(
            document.DocumentPaginator,
            $"Dream Dough PH Receipt {Path.GetFileNameWithoutExtension(receiptImagePath)}");
        return true;
    }

    private static void ValidatePrintQueue(PrintQueue printQueue)
    {
        printQueue.Refresh();

        if (printQueue.IsOffline)
        {
            throw new InvalidOperationException($"Selected printer '{printQueue.FullName}' is offline.");
        }

        if (printQueue.IsPaused)
        {
            throw new InvalidOperationException($"Selected printer '{printQueue.FullName}' is paused.");
        }

        if (printQueue.IsInError || printQueue.NeedUserIntervention)
        {
            throw new InvalidOperationException($"Selected printer '{printQueue.FullName}' needs attention.");
        }
    }

    private static IDocument CreateDocument(OrderCreateRequest request, byte[] qrCodeBytes)
    {
        var vatBreakdown = CalculateVatBreakdown(request.Total);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(CalculateReceiptPageSize(request));
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

                    column.Item().Column(details =>
                    {
                        details.Spacing(4);
                        AddDetail(details, "Customer", request.CustomerName);
                        AddDetail(details, "Phone", request.CustomerPhone);
                        AddDetail(details, "Pickup", $"{request.PickupDate} {request.PickupTime}");
                        AddDetail(details, "Date", request.Date);
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
                        totals.Item().Text($"Original Amount: {vatBreakdown.OriginalAmount.ToPeso()}");
                        totals.Item().Text($"VAT (12% included): {vatBreakdown.VatAmount.ToPeso()}");
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

                    column.Item().AlignCenter().Width(150).Image(qrCodeBytes).FitWidth();

                    column.Item().PaddingTop(4).AlignCenter().Text("Please confirm this receipt before sending the order.")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                });
            });
        });
    }

    internal static VatBreakdown CalculateVatBreakdown(decimal total)
    {
        var vatAmount = decimal.Round(total * VatRate, 2, MidpointRounding.AwayFromZero);
        return new VatBreakdown(total - vatAmount, vatAmount);
    }

    private static PageSize CalculateReceiptPageSize(OrderCreateRequest request)
    {
        var pageHeight = 650f;
        pageHeight += request.Items.Sum(EstimateItemRowHeight);

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            pageHeight += 45f + EstimateWrappedLineCount(request.Notes, 58) * 15f;
        }

        return new PageSize(ReceiptPageWidth, Math.Max(MinimumReceiptPageHeight, pageHeight));
    }

    private static float EstimateItemRowHeight(OrderItemDto item)
    {
        return 22f + EstimateWrappedLineCount(item.Name, 22) * 11f;
    }

    private static int EstimateWrappedLineCount(string text, int charactersPerLine)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 1;
        }

        return Math.Max(1, (int)Math.Ceiling(text.Length / (double)charactersPerLine));
    }

    private static FixedDocument CreatePrintableDocument(IReadOnlyList<BitmapImage> pages, System.Windows.Size pageSize)
    {
        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = pageSize;

        foreach (var page in pages)
        {
            var fixedPage = new FixedPage
            {
                Width = pageSize.Width,
                Height = pageSize.Height
            };

            var image = new System.Windows.Controls.Image
            {
                Source = page,
                Width = pageSize.Width,
                Height = pageSize.Height,
                Stretch = System.Windows.Media.Stretch.Uniform
            };

            FixedPage.SetLeft(image, 0);
            FixedPage.SetTop(image, 0);
            fixedPage.Children.Add(image);

            var pageContent = new PageContent();
            ((System.Windows.Markup.IAddChild)pageContent).AddChild(fixedPage);
            document.Pages.Add(pageContent);
        }

        return document;
    }

    private static System.Windows.Size GetPrintablePageSize(PrintDialog printDialog, PrintQueue printQueue)
    {
        if (printDialog.PrintableAreaWidth > 0 && printDialog.PrintableAreaHeight > 0)
        {
            return new System.Windows.Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);
        }

        var mediaSize = printQueue.DefaultPrintTicket.PageMediaSize;
        if (mediaSize?.Width > 0 && mediaSize?.Height > 0)
        {
            return new System.Windows.Size(mediaSize.Width.Value, mediaSize.Height.Value);
        }

        return new System.Windows.Size(816, 1056);
    }

    private static IEnumerable<byte[]> GenerateReceiptImages(OrderCreateRequest request)
    {
        var qrCodeBytes = CreateQrCodeBytes(request);
        var document = CreateDocument(request, qrCodeBytes);
        return document.GenerateImages(new ImageGenerationSettings
        {
            RasterDpi = 160
        });
    }

    private static BitmapImage LoadReceiptImage(string receiptImagePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(receiptImagePath, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
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
        var vatBreakdown = CalculateVatBreakdown(request.Total);
        var items = string.Join(", ", request.Items.Select(item => $"{item.Quantity}x {item.Name}"));
        var builder = new StringBuilder();
        builder.AppendLine("Dream Dough PH");
        builder.AppendLine($"Order #: {request.OrderNumber.ToString("D3", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"Customer: {request.CustomerName}");
        builder.AppendLine($"Phone: {request.CustomerPhone}");
        builder.AppendLine($"Pickup: {request.PickupDate} {request.PickupTime}");
        builder.AppendLine($"Items: {items}");
        builder.AppendLine($"Original Amount: {vatBreakdown.OriginalAmount.ToPeso()}");
        builder.AppendLine($"VAT (12% included): {vatBreakdown.VatAmount.ToPeso()}");
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

public readonly record struct VatBreakdown(decimal OriginalAmount, decimal VatAmount);
