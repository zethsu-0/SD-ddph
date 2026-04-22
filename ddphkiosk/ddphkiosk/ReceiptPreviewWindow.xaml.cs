using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ddphkiosk;

public partial class ReceiptPreviewWindow : Window
{
    public ReceiptPreviewWindow(IReadOnlyList<BitmapImage> previewPages)
    {
        InitializeComponent();
        DataContext = new ReceiptPreviewViewModel(previewPages);
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        DragMove();
    }
}

public sealed class ReceiptPreviewViewModel
{
    public ReceiptPreviewViewModel(IReadOnlyList<BitmapImage> previewPages)
    {
        PreviewPages = previewPages;
    }

    public IReadOnlyList<BitmapImage> PreviewPages { get; }
}
