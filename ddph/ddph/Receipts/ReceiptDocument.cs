using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ddph.Receipts;

public sealed class ReceiptDocument : IDocument
{
    private readonly IReadOnlyList<CartItem> _items;
    private readonly decimal _subtotal;
    private readonly decimal _discount;
    private readonly decimal _total;
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
            page.Size(PageSizes.A4);
            page.Margin(42);
            page.DefaultTextStyle(style => style.FontSize(13).FontFamily(Fonts.Arial).FontColor("#3F3431"));

            page.Content().Column(column =>
            {
                column.Spacing(12);
                column.Item().AlignCenter().Text("Dream Dough PH").FontSize(26).SemiBold().FontColor("#5C4843");
                column.Item().AlignCenter().Text("Order Receipt").FontSize(13).FontColor("#6D625F");
                column.Item().LineHorizontal(1);

                column.Item().Row(row =>
                {
                    row.RelativeItem().Column(details =>
                    {
                        details.Spacing(4);
                        details.Item().Text($"Reference: {_reference}").FontSize(12);
                        details.Item().Text($"Date: {_createdAt:yyyy-MM-dd HH:mm}").FontSize(12);
                    });
                });

                column.Item().LineHorizontal(1);

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.ConstantColumn(46);
                        columns.ConstantColumn(94);
                        columns.ConstantColumn(104);
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
                        table.Cell().Element(BodyCell).AlignRight().Text(item.Price.ToString("C"));
                        table.Cell().Element(BodyCell).AlignRight().Text((item.Price * item.Qty).ToString("C"));
                    }
                });

                column.Item().LineHorizontal(1);
                AddAmountRow(column, "Subtotal", _subtotal, 13);

                if (_discount > 0)
                {
                    AddAmountRow(column, _discountLabel, -_discount, 13);
                }

                column.Item().AlignRight().Text(text =>
                {
                    text.Span("Total: ").FontSize(16).SemiBold();
                    text.Span(_total.ToString("C")).FontSize(20).SemiBold().FontColor("#5C4843");
                });
                AddAmountRow(column, "Payment", _payment, 13);
                AddAmountRow(column, "Change", _change, 13);

                column.Item().PaddingTop(18).AlignCenter().Width(120).Image(_qrCodeImage);
                column.Item().AlignCenter().Text("Scan for receipt reference").FontSize(12);
                column.Item().PaddingTop(10).AlignCenter().Text("Thank you!").FontSize(16).SemiBold();
            });
        });
    }

    private static IContainer HeaderCell(IContainer container)
    {
        return container.Background("#EEEEEE").PaddingVertical(6).PaddingHorizontal(5);
    }

    private static IContainer BodyCell(IContainer container)
    {
        return container.BorderBottom(1).BorderColor("#EEEEEE").PaddingVertical(8).PaddingHorizontal(5);
    }

    private static void AddAmountRow(ColumnDescriptor column, string label, decimal amount, int fontSize)
    {
        column.Item().AlignRight().Text(text =>
        {
            text.Span($"{label}: ").FontSize(fontSize).SemiBold();
            text.Span(amount.ToString("C")).FontSize(fontSize);
        });
    }

    private byte[] CreateQrCodeImage()
    {
        var payload = $"DDPH|REF:{_reference}|TOTAL:{_total:0.00}|DATE:{_createdAt:yyyy-MM-dd HH:mm}";
        using var qrCodeData = QRCodeGenerator.GenerateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(12);
    }
}
