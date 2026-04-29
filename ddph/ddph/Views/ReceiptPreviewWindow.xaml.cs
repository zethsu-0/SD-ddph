using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ddph.Views
{
    public partial class ReceiptPreviewWindow : Window
    {
        public ReceiptPreviewWindow(string filePath, IReadOnlyList<byte[]> previewImages)
        {
            InitializeComponent();
            ReceiptPathTextBlock.Text = filePath;
            PreviewPagesItemsControl.ItemsSource = previewImages.Select(CreateImageSource).ToList();
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
