using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ddph.Views
{
    public partial class DiscountWindow : Window
    {
        private readonly Button[] _presetButtons;
        private decimal? _selectedDiscount;
        private Button? _selectedShortcutButton;

        public DiscountWindow(decimal currentDiscountRate, string? currentDiscountType)
        {
            InitializeComponent();
            _presetButtons =
            [
                Discount5Button,
                Discount10Button,
                Discount15Button,
                Discount20Button,
                Discount25Button,
                Discount50Button
            ];

            InitializeState(currentDiscountRate, currentDiscountType);
        }

        public decimal SelectedDiscountRate { get; private set; }
        public string? SelectedCustomerType { get; private set; }
        public bool WasCleared { get; private set; }

        private void InitializeState(decimal currentDiscountRate, string? currentDiscountType)
        {
            if (currentDiscountRate > 0)
            {
                _selectedDiscount = currentDiscountRate;
                var presetButton = _presetButtons.FirstOrDefault(button => button.Tag?.ToString() == currentDiscountRate.ToString("0.##", CultureInfo.InvariantCulture));
                if (presetButton != null)
                {
                    ApplyPresetHighlight(presetButton);
                }
                else
                {
                    CustomDiscountTextBox.Text = currentDiscountRate.ToString("0.##", CultureInfo.InvariantCulture);
                }
            }

            if (NormalizeShortcutType(currentDiscountType) == "pwd")
            {
                _selectedShortcutButton = PwdButton;
            }
            else if (NormalizeShortcutType(currentDiscountType) == "senior")
            {
                _selectedShortcutButton = SeniorButton;
            }

            UpdateShortcutButtons();
            UpdatePreview();
        }

        private void PwdButton_Click(object sender, RoutedEventArgs e)
        {
            SelectDefaultDiscount(PwdButton);
        }

        private void SeniorButton_Click(object sender, RoutedEventArgs e)
        {
            SelectDefaultDiscount(SeniorButton);
        }

        private void PresetDiscount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            if (!decimal.TryParse(button.Tag?.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var rate))
            {
                return;
            }

            _selectedDiscount = rate;
            _selectedShortcutButton = null;
            CustomDiscountTextBox.TextChanged -= CustomDiscountTextBox_TextChanged;
            CustomDiscountTextBox.Text = string.Empty;
            CustomDiscountTextBox.TextChanged += CustomDiscountTextBox_TextChanged;
            ApplyPresetHighlight(button);
            UpdateShortcutButtons();
            UpdatePreview();
        }

        private void CustomDiscountTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ResetPresetHighlight();

            if (decimal.TryParse(CustomDiscountTextBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate) &&
                rate > 0 &&
                rate <= 100)
            {
                _selectedDiscount = rate;
                _selectedShortcutButton = null;
            }
            else
            {
                _selectedDiscount = null;
            }

            UpdateShortcutButtons();
            UpdatePreview();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedDiscount is null)
            {
                MessageBox.Show("Select discount rate.", "Discount", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedCustomerType = _selectedShortcutButton == PwdButton
                ? "pwd"
                : _selectedShortcutButton == SeniorButton
                    ? "senior"
                    : "discount";
            SelectedDiscountRate = _selectedDiscount.Value;
            DialogResult = true;
            Close();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            WasCleared = true;
            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void UpdateShortcutButtons()
        {
            SetButtonPalette(PwdButton, _selectedShortcutButton == PwdButton);
            SetButtonPalette(SeniorButton, _selectedShortcutButton == SeniorButton);
        }

        private void SelectDefaultDiscount(Button shortcutButton)
        {
            _selectedDiscount = 20m;
            _selectedShortcutButton = shortcutButton;
            CustomDiscountTextBox.TextChanged -= CustomDiscountTextBox_TextChanged;
            CustomDiscountTextBox.Text = string.Empty;
            CustomDiscountTextBox.TextChanged += CustomDiscountTextBox_TextChanged;
            ApplyPresetHighlight(Discount20Button);
            UpdateShortcutButtons();
            UpdatePreview();
        }

        private void ApplyPresetHighlight(Button activeButton)
        {
            foreach (var button in _presetButtons)
            {
                SetButtonPalette(button, button == activeButton);
            }
        }

        private void ResetPresetHighlight()
        {
            foreach (var button in _presetButtons)
            {
                SetButtonPalette(button, false);
            }
        }

        private void SetButtonPalette(Button button, bool isActive)
        {
            button.Background = isActive ? BrushFrom("#E1F5EE") : Brushes.White;
            button.BorderBrush = isActive ? BrushFrom("#1D9E75") : BrushFrom("#26D4C3BE");
            button.Foreground = isActive ? BrushFrom("#0F6E56") : (Brush)FindResource("ThemeInkBrush");
        }

        private void UpdatePreview()
        {
            if (_selectedDiscount is null)
            {
                PreviewTextBlock.Text = "select above";
                return;
            }

            PreviewTextBlock.Text = $"{_selectedDiscount:0.##}% off";
        }

        private static string? NormalizeShortcutType(string? customerType)
        {
            if (string.Equals(customerType, "PWD", StringComparison.OrdinalIgnoreCase))
            {
                return "pwd";
            }

            if (string.Equals(customerType, "Senior", StringComparison.OrdinalIgnoreCase))
            {
                return "senior";
            }

            return string.IsNullOrWhiteSpace(customerType) ? null : customerType.Trim().ToLowerInvariant();
        }

        private static Brush BrushFrom(string color)
        {
            return (SolidColorBrush)new BrushConverter().ConvertFrom(color)!;
        }
    }
}
