using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System.Windows.Markup;

namespace ddph.Receipts;

public static class ReceiptPrintService
{
    public static bool Print(IReadOnlyList<byte[]> previewImages, string documentName)
    {
        if (previewImages.Count == 0)
        {
            return false;
        }

        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true)
        {
            return false;
        }

        ValidatePrintQueue(printDialog.PrintQueue);

        var pageSize = GetPrintablePageSize(printDialog, printDialog.PrintQueue);
        var fixedDocument = CreatePrintableDocument(
            previewImages.Select(CreateImageSource).ToList(),
            pageSize);

        printDialog.PrintDocument(
            fixedDocument.DocumentPaginator,
            $"Dream Dough PH Receipt {documentName}");

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

    private static FixedDocument CreatePrintableDocument(IReadOnlyList<BitmapImage> pages, Size pageSize)
    {
        var fixedDocument = new FixedDocument();
        fixedDocument.DocumentPaginator.PageSize = pageSize;

        foreach (var pageImage in pages)
        {
            var fixedPage = new FixedPage
            {
                Width = pageSize.Width,
                Height = pageSize.Height
            };

            var image = new Image
            {
                Source = pageImage,
                Stretch = System.Windows.Media.Stretch.Uniform,
                Width = pageSize.Width,
                Height = pageSize.Height
            };

            FixedPage.SetLeft(image, 0);
            FixedPage.SetTop(image, 0);
            fixedPage.Children.Add(image);

            var pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(fixedPage);
            fixedDocument.Pages.Add(pageContent);
        }

        return fixedDocument;
    }

    private static Size GetPrintablePageSize(PrintDialog printDialog, PrintQueue printQueue)
    {
        if (printDialog.PrintableAreaWidth > 0 && printDialog.PrintableAreaHeight > 0)
        {
            return new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);
        }

        var mediaSize = printQueue.DefaultPrintTicket.PageMediaSize;
        if (mediaSize?.Width > 0 && mediaSize?.Height > 0)
        {
            return new Size(mediaSize.Width.Value, mediaSize.Height.Value);
        }

        return new Size(816, 1056);
    }

    private static BitmapImage CreateImageSource(byte[] imageBytes)
    {
        using var stream = new MemoryStream(imageBytes);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
