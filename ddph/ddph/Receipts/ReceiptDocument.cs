using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ddph.Receipts;

public sealed class ReceiptDocument : IDocument
{
    private const float ReceiptPageWidth = 420f;
    private const float MinimumReceiptPageHeight = 595f;

    private readonly IReadOnlyList<CartItem> _items;
    private readonly decimal _subtotal;
    private readonly decimal _discount;
    private readonly decimal _total;
    private readonly decimal _vatableSales;
    private readonly decimal _vatAmount;
    private readonly decimal _payment;
    private readonly decimal _change;
    private readonly string _discountLabel;
    private readonly string _reference;
    private readonly DateTime _createdAt;
    private readonly byte[] _qrCodeImage;

    public ReceiptDocument(
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
        _items = items;
        _subtotal = subtotal;
        _discount = discount;
        _total = total;
        _vatableSales = vatableSales;
        _vatAmount = vatAmount;
        _payment = payment;
        _change = change;
        _discountLabel = discountLabel;
        _reference = reference;
        _createdAt = createdAt;
        _qrCodeImage = CreateQrCodeImage();
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(CalculateReceiptPageSize());
            page.Margin(28);
            page.DefaultTextStyle(style => style.FontSize(10).FontFamily(Fonts.Arial).FontColor(Colors.Grey.Darken4));

            page.Content().Column(column =>
            {
                column.Spacing(9);
                column.Item().AlignCenter().Text("Dream Dough PH").FontSize(22).Bold().FontColor(Colors.Brown.Darken2);
                column.Item().AlignCenter().Text("Order Receipt").FontSize(11).FontColor(Colors.Grey.Darken2);
                column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(details =>
                    {
                        details.Spacing(4);
                        details.Item().Text($"Reference: {_reference}");
                        details.Item().Text($"Date: {_createdAt:yyyy-MM-dd HH:mm}");
                    });
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

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Item").SemiBold();
                        header.Cell().Element(HeaderCell).AlignCenter().Text("Qty").SemiBold();
                        header.Cell().Element(HeaderCell).AlignRight().Text("Price").SemiBold();
                        header.Cell().Element(HeaderCell).AlignRight().Text("Total").SemiBold();
                    });

                    foreach (var item in _items)
                    {
                        table.Cell().Element(BodyCell).Text(item.Item);
                        table.Cell().Element(BodyCell).AlignCenter().Text(item.Qty.ToString());
                        table.Cell().Element(BodyCell).AlignRight().Text(ToPeso(item.Price));
                        table.Cell().Element(BodyCell).AlignRight().Text(ToPeso(item.Price * item.Qty));
                    }
                });

                column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                AddAmountRow(column, "Subtotal", _subtotal, 10);

                if (_discount > 0)
                {
                    AddAmountRow(column, _discountLabel, -_discount, 10);
                }

                column.Item().AlignRight().Text(text =>
                {
                    text.Span("Total: ").FontSize(15).Bold();
                    text.Span(ToPeso(_total)).FontSize(15).Bold().FontColor(Colors.Brown.Darken2);
                });

                column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                AddAmountRow(column, "VATable Sales", _vatableSales, 10);
                AddAmountRow(column, "12% VAT", _vatAmount, 10);
                column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                AddAmountRow(column, "Payment", _payment, 10);
                AddAmountRow(column, "Change", _change, 10);

                column.Item().PaddingTop(4).AlignCenter().Width(150).Image(_qrCodeImage).FitWidth();
                column.Item().AlignCenter().Text("Scan for receipt reference").FontSize(9).FontColor(Colors.Grey.Darken1);
                column.Item().PaddingTop(4).AlignCenter().Text("Thank you!").FontSize(12).SemiBold();
            });
        });
    }

    private static IContainer HeaderCell(IContainer container)
    {
        return container.Background(Colors.Grey.Lighten3).PaddingVertical(5).PaddingHorizontal(4);
    }

    private static IContainer BodyCell(IContainer container)
    {
        return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten3).PaddingVertical(5).PaddingHorizontal(4);
    }

    private static void AddAmountRow(ColumnDescriptor column, string label, decimal amount, int fontSize)
    {
        column.Item().AlignRight().Text(text =>
        {
            text.Span($"{label}: ").FontSize(fontSize).SemiBold();
            text.Span(ToPeso(amount)).FontSize(fontSize);
        });
    }

    private PageSize CalculateReceiptPageSize()
    {
        var pageHeight = 595f;
        pageHeight += _items.Sum(EstimateItemRowHeight);

        if (_discount > 0)
        {
            pageHeight += 15f;
        }

        return new PageSize(ReceiptPageWidth, Math.Max(MinimumReceiptPageHeight, pageHeight));
    }

    private static float EstimateItemRowHeight(CartItem item)
    {
        return 22f + EstimateWrappedLineCount(item.Item, 22) * 11f;
    }

    private static int EstimateWrappedLineCount(string text, int charactersPerLine)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 1;
        }

        return Math.Max(1, (int)Math.Ceiling(text.Length / (double)charactersPerLine));
    }

    private static string ToPeso(decimal value)
    {
        return $"PHP {value:N2}";
    }

    private byte[] CreateQrCodeImage()
    {
        var payload = $"DDPH|REF:{_reference}|TOTAL:{_total:0.00}|DATE:{_createdAt:yyyy-MM-dd HH:mm}";
        using var qrCodeData = QRCodeGenerator.GenerateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(12);
    }
}
