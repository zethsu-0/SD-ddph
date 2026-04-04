using System.Windows;
using ddph.ViewModels;

namespace ddph
{
    public partial class CustomItemsWindow : Window
    {
        public CustomItemsWindow()
        {
            InitializeComponent();
            DataContext = new CustomItemsViewModel();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
