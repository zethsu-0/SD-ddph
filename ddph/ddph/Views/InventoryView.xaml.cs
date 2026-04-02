using System.Windows.Controls;
using ddph.ViewModels;

namespace ddph.Views
{
    public partial class InventoryView : UserControl
    {
        public InventoryView()
        {
            InitializeComponent();
            DataContext = new InventoryViewModel();
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {

        }
    }
}
