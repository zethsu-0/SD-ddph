using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        private void DigitsOnlyTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = e.Text.Any(character => !char.IsDigit(character));
        }

        private void LettersOnlyTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = e.Text.Any(character =>
                !char.IsLetter(character) &&
                !char.IsWhiteSpace(character) &&
                character != '\'' &&
                character != '-');
        }
    }
}
