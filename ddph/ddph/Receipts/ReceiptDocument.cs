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
            page.Margin(22);
            page.DefaultTextStyle(style => style.FontSize(25).FontFamily(Fonts.Arial));

            page.Content().Column(column =>
            {
                column.Spacing(8);
                column.Item().AlignCenter().Text("DDPH").FontSize(28).SemiBold();
                column.Item().AlignCenter().Text($"Ref: {_reference}").FontSize(14);
                column.Item().AlignCenter().Text(_createdAt.ToString("yyyy-MM-dd HH:mm"));
                column.Item().LineHorizontal(1);

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.ConstantColumn(90);
                        columns.ConstantColumn(160);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Item").SemiBold();
                        header.Cell().AlignRight().Text("Qty").SemiBold();
                        header.Cell().AlignRight().Text("Amount").SemiBold();
                    });

                    foreach (var item in _items)
                    {
                        table.Cell().Text(item.Item);
                        table.Cell().AlignRight().Text(item.Qty.ToString());
                        table.Cell().AlignRight().Text((item.Price * item.Qty).ToString("C"));
                    }
                });

                column.Item().LineHorizontal(1);
                AddAmountRow(column, "Subtotal", _subtotal);

                if (_discount > 0)
                {
                    AddAmountRow(column, _discountLabel, -_discount);
                }

                column.Item().AlignRight().Text(text =>
                {
                    text.Span("Total: ").SemiBold();
                    text.Span(_total.ToString("C")).FontSize(20).SemiBold();
                });
                AddAmountRow(column, "Payment", _payment);
                AddAmountRow(column, "Change", _change);

                column.Item().PaddingTop(10).AlignCenter().Width(130).Image(_qrCodeImage);
                column.Item().AlignCenter().Text("Scan for receipt reference").FontSize(12);
                column.Item().PaddingTop(16).AlignCenter().Text("Thank you!").FontSize(18).SemiBold();
            });
        });
    }

    private static void AddAmountRow(ColumnDescriptor column, string label, decimal amount)
    {
        column.Item().AlignRight().Text(text =>
        {
            text.Span($"{label}: ").SemiBold();
            text.Span(amount.ToString("C"));
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
