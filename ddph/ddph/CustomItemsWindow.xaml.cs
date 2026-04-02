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
    }
}
