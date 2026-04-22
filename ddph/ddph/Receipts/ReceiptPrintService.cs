using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System.Windows.Markup;
using System.Windows.Xps;

namespace ddph.Receipts;

public static class ReceiptPrintService
{
    public static void Print(IReadOnlyList<byte[]> previewImages, string documentName)
    {
        if (previewImages.Count == 0)
        {
            return;
        }

        var printQueue = LocalPrintServer.GetDefaultPrintQueue();
        var fixedDocument = new FixedDocument();

        foreach (var previewImage in previewImages)
        {
            var imageSource = CreateImageSource(previewImage);
            var printableAreaWidth = printQueue.DefaultPrintTicket.PageMediaSize?.Width ?? imageSource.Width;
            var printableAreaHeight = printQueue.DefaultPrintTicket.PageMediaSize?.Height ?? imageSource.Height;

            var image = new Image
            {
                Source = imageSource,
                Stretch = System.Windows.Media.Stretch.Uniform,
                Width = printableAreaWidth,
                Height = printableAreaHeight
            };

            var page = new FixedPage
            {
                Width = printableAreaWidth,
                Height = printableAreaHeight
            };
            page.Children.Add(image);

            var pageContent = new PageContent();
            ((IAddChild)pageContent).AddChild(page);
            fixedDocument.Pages.Add(pageContent);
        }

        var writer = PrintQueue.CreateXpsDocumentWriter(printQueue);
        writer.Write(fixedDocument.DocumentPaginator, printQueue.DefaultPrintTicket);
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
