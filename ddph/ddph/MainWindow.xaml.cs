using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Linq;
using System.IO;
using ddph.Data;
using ddph.ViewModels;
using ddph.Views;
using ddph.Receipts;

namespace ddph
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private UIElement? _inventoryContent;
        private UIElement? _ordersContent;
        private UIElement? _customContent;
        private UIElement? _settingsContent;
        private readonly Brush _activeNavBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
        private readonly Brush _inactiveNavBrush = Brushes.Transparent;

        public MainWindow()
        {
            InitializeComponent();
            var viewModel = new MainWindowViewModel();
            viewModel.PaymentFocusRequested += FocusPaymentTextBox;
            viewModel.ReceiptGenerated += ShowReceiptPreview;
            DataContext = viewModel;
            LoggedInUserText.Text = AuthSessionStore.CurrentUsername;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ShowInventoryTab();
        }

        private void OrdersButton_Click(object sender, RoutedEventArgs e)
        {
            ShowOrdersTab();
        }

        private void QuickViewButton_Click(object sender, RoutedEventArgs e)
        {
            var quickViewWindow = new QuickOrderViewWindow
            {
                Owner = this
            };
            quickViewWindow.ShowDialog();
        }

        private void CustomItemsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowCustomTab();
        }

        private void RegisterTab_Click(object sender, RoutedEventArgs e)
        {
            ShowRegisterTab();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsTab();
        }

        private void FocusPaymentTextBox()
        {
            PaymentTextBox.Focus();
            PaymentTextBox.SelectAll();
        }

        private void ShowReceiptPreview(ReceiptPdfResult receipt)
        {
            var previewWindow = new ReceiptPreviewWindow(receipt.FilePath, receipt.PreviewImages)
            {
                Owner = this
            };
            previewWindow.ShowDialog();
        }

        private void PaymentTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                return;
            }

            var nextText = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength);
            nextText = nextText.Insert(textBox.CaretIndex, e.Text);
            e.Handled = nextText == "0" || !decimal.TryParse(nextText, out _);
        }

        private void CartQtyTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = e.Text.Any(character => !char.IsDigit(character));
        }

        private void CartQtyTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text) ||
                e.DataObject.GetData(DataFormats.Text) is not string pastedText ||
                !int.TryParse(pastedText, out var quantity) ||
                quantity < 0)
            {
                e.CancelCommand();
            }
        }

        private void CartQtyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitCartQuantityEdit(sender as TextBox);
        }

        private void CartQtyTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            CommitCartQuantityEdit(sender as TextBox);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            AuthSessionStore.Forget();
            var loginWindow = new LoginWindow();
            Application.Current.MainWindow = loginWindow;
            loginWindow.Show();
            Close();
        }

        private void DiscountButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var discountWindow = new DiscountWindow(viewModel.DiscountRate, viewModel.HasDiscount ? viewModel.DiscountTypeLabel : null)
            {
                Owner = this
            };

            var result = discountWindow.ShowDialog();
            if (result == true)
            {
                viewModel.ApplyDiscount(discountWindow.SelectedDiscountRate, discountWindow.SelectedCustomerType);
                return;
            }

            if (discountWindow.WasCleared)
            {
                viewModel.ClearDiscount();
            }
        }

        private void ShowRegisterTab()
        {
            PageTitleText.Text = "Register";
            SetActiveNavButton(RegisterNavButton);
            RegisterContent.Visibility = Visibility.Visible;
            TabContentHost.Visibility = Visibility.Collapsed;
            TabContentHost.Content = null;
            MainSearchPanel.Visibility = Visibility.Visible;

            if (DataContext is MainWindowViewModel viewModel && viewModel.RefreshProductsCommand.CanExecute(null))
            {
                viewModel.RefreshProductsCommand.Execute(null);
            }
        }

        private void ShowInventoryTab()
        {
            _inventoryContent ??= new InventoryView();
            PageTitleText.Text = "Inventory";
            SetActiveNavButton(InventoryNavButton);
            ShowEmbeddedTab(_inventoryContent);
        }

        private void ShowOrdersTab()
        {
            _ordersContent ??= CreateEmbeddedWindowContent(new OnlineOrders());
            PageTitleText.Text = "Orders";
            SetActiveNavButton(OrdersNavButton);
            ShowEmbeddedTab(_ordersContent);
        }

        private void ShowCustomTab()
        {
            _customContent ??= CreateEmbeddedWindowContent(new CustomItemsWindow());
            PageTitleText.Text = "Custom";
            SetActiveNavButton(CustomNavButton);
            ShowEmbeddedTab(_customContent);
        }

        private void ShowSettingsTab()
        {
            _settingsContent ??= CreateSettingsContent();
            PageTitleText.Text = "Settings";
            SetActiveNavButton(SettingsNavButton);
            ShowEmbeddedTab(_settingsContent);
        }

        private void ShowEmbeddedTab(UIElement? content)
        {
            if (content == null)
            {
                return;
            }

            RegisterContent.Visibility = Visibility.Collapsed;
            TabContentHost.Content = content;
            TabContentHost.Visibility = Visibility.Visible;
            MainSearchPanel.Visibility = Visibility.Collapsed;
        }

        private static UIElement? CreateEmbeddedWindowContent(Window sourceWindow)
        {
            if (sourceWindow.Content is not UIElement content)
            {
                return null;
            }

            sourceWindow.Content = null;

            if (content is FrameworkElement element)
            {
                element.DataContext = sourceWindow.DataContext;
            }

            if (content is Grid rootGrid && rootGrid.Children.Count > 0 && rootGrid.Children[0] is Button overlayButton)
            {
                overlayButton.Visibility = Visibility.Collapsed;
            }

            return content;
        }

        private UIElement CreateSettingsContent()
        {
            var employeeRepository = new EmployeeRepository();
            var employeeList = new StackPanel();
            var employeeListStatusTextBlock = new TextBlock
            {
                Margin = new Thickness(0, 10, 0, 0),
                Foreground = (Brush)FindResource("ThemeInkSoftBrush")
            };
            string? editingEmployeeUsername = null;
            var employeeNameTextBox = CreateSettingsTextBox();
            var employeeIdTextBox = CreateSettingsTextBox();
            var employeePinBox = new PasswordBox
            {
                Height = 42,
                Width = 260,
                Padding = new Thickness(12, 0, 12, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = Brushes.White,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
            var employeeStatusTextBlock = new TextBlock
            {
                Margin = new Thickness(0, 12, 0, 0),
                Foreground = (Brush)FindResource("ThemeInkSoftBrush")
            };
            var saveEmployeeButton = new Button
            {
                Width = 180,
                Height = 48,
                Margin = new Thickness(0, 18, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = (Brush)FindResource("ToastBrush"),
                Foreground = Brushes.White,
                Content = "Add Employee"
            };
            saveEmployeeButton.Click += async (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(employeeNameTextBox.Text) ||
                    string.IsNullOrWhiteSpace(employeeIdTextBox.Text) ||
                    editingEmployeeUsername == null && string.IsNullOrWhiteSpace(employeePinBox.Password))
                {
                    employeeStatusTextBlock.Text = "Complete employee details.";
                    return;
                }

                try
                {
                    saveEmployeeButton.IsEnabled = false;
                    if (editingEmployeeUsername == null)
                    {
                        await employeeRepository.AddEmployeeAsync(
                            employeeNameTextBox.Text,
                            employeeIdTextBox.Text,
                            employeePinBox.Password);
                        employeeStatusTextBlock.Text = "Employee saved.";
                    }
                    else
                    {
                        await employeeRepository.UpdateEmployeeAsync(
                            editingEmployeeUsername,
                            employeeNameTextBox.Text,
                            employeeIdTextBox.Text,
                            employeePinBox.Password);
                        employeeStatusTextBlock.Text = "Employee updated.";
                    }

                    editingEmployeeUsername = null;
                    employeeNameTextBox.Clear();
                    employeeIdTextBox.Clear();
                    employeePinBox.Clear();
                    saveEmployeeButton.Content = "Add Employee";
                    await LoadEmployeesAsync();
                }
                catch (Exception ex)
                {
                    employeeStatusTextBlock.Text = ex.Message;
                }
                finally
                {
                    saveEmployeeButton.IsEnabled = true;
                }
            };

            async Task LoadEmployeesAsync()
            {
                try
                {
                    employeeList.Children.Clear();
                    foreach (var employee in await employeeRepository.GetEmployeesAsync())
                    {
                        var editButton = CreateSmallSettingsButton("Edit");
                        editButton.Visibility = AuthSessionStore.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
                        editButton.Click += (_, _) =>
                        {
                            editingEmployeeUsername = employee.Username;
                            employeeNameTextBox.Text = employee.DisplayName;
                            employeeIdTextBox.Text = employee.Username;
                            employeePinBox.Clear();
                            saveEmployeeButton.Content = "Save Employee";
                            employeeStatusTextBlock.Text = "Editing employee. Leave password blank to keep it.";
                        };

                        var removeButton = CreateSmallSettingsButton("Remove");
                        removeButton.Visibility = AuthSessionStore.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
                        removeButton.Click += async (_, _) =>
                        {
                            var result = MessageBox.Show(
                                $"Remove {employee.DisplayName}?",
                                "Remove Employee",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);

                            if (result != MessageBoxResult.Yes)
                            {
                                return;
                            }

                            try
                            {
                                await employeeRepository.DeleteEmployeeAsync(employee.Username);
                                employeeStatusTextBlock.Text = "Employee removed.";
                                if (editingEmployeeUsername == employee.Username)
                                {
                                    editingEmployeeUsername = null;
                                    employeeNameTextBox.Clear();
                                    employeeIdTextBox.Clear();
                                    employeePinBox.Clear();
                                    saveEmployeeButton.Content = "Add Employee";
                                }

                                await LoadEmployeesAsync();
                            }
                            catch (Exception ex)
                            {
                                employeeStatusTextBlock.Text = ex.Message;
                            }
                        };

                        var actionsPanel = new StackPanel
                        {
                            Margin = new Thickness(0, 10, 0, 0),
                            Orientation = Orientation.Horizontal,
                            Children =
                            {
                                editButton,
                                removeButton
                            }
                        };

                        employeeList.Children.Add(new Border
                        {
                            Margin = new Thickness(0, 0, 0, 8),
                            Padding = new Thickness(12),
                            Background = Brushes.White,
                            CornerRadius = new CornerRadius(12),
                            Child = new StackPanel
                            {
                                Children =
                                {
                                    new TextBlock
                                    {
                                        Text = employee.DisplayName,
                                        FontWeight = FontWeights.SemiBold,
                                        Foreground = (Brush)FindResource("ThemeInkBrush")
                                    },
                                    new TextBlock
                                    {
                                        Margin = new Thickness(0, 4, 0, 0),
                                        Text = employee.Username,
                                        Foreground = (Brush)FindResource("ThemeInkSoftBrush")
                                    },
                                    actionsPanel
                                }
                            }
                        });
                    }

                    employeeListStatusTextBlock.Text = employeeList.Children.Count == 0 ? "No employees yet." : string.Empty;
                }
                catch (Exception ex)
                {
                    employeeListStatusTextBlock.Text = ex.Message;
                }
            }

            var logoutButton = new Button
            {
                Width = 180,
                Height = 48,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = (Brush)FindResource("ToastBrush"),
                Foreground = Brushes.White,
                Content = "Logout"
            };
            logoutButton.Click += LogoutButton_Click;

            var accountCard = new Border
            {
                Padding = new Thickness(28),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Background = (Brush)FindResource("ThemeSurfaceBrush"),
                CornerRadius = new CornerRadius(24),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Account",
                            FontSize = 24,
                            FontWeight = FontWeights.Bold,
                            Foreground = (Brush)FindResource("ThemeInkBrush")
                        },
                        new TextBlock
                        {
                            Margin = new Thickness(0, 8, 0, 18),
                            Text = $"Signed in as {AuthSessionStore.CurrentUsername}",
                            Foreground = (Brush)FindResource("ThemeInkSoftBrush")
                        },
                        logoutButton
                    }
                }
            };

            var employeeCard = new Border
            {
                Padding = new Thickness(28),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Background = (Brush)FindResource("ThemeSurfaceBrush"),
                CornerRadius = new CornerRadius(24),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Employees",
                            FontSize = 24,
                            FontWeight = FontWeights.Bold,
                            Foreground = (Brush)FindResource("ThemeInkBrush")
                        },
                        new TextBlock
                        {
                            Margin = new Thickness(0, 8, 0, 18),
                            Text = AuthSessionStore.IsAdmin ? "Add staff login" : "Staff list",
                            Foreground = (Brush)FindResource("ThemeInkSoftBrush")
                        },
                        CreateSettingsLabel("Name"),
                        employeeNameTextBox,
                        CreateSettingsLabel("Staff ID"),
                        employeeIdTextBox,
                        CreateSettingsLabel("Pin/Password"),
                        employeePinBox,
                        saveEmployeeButton,
                        employeeStatusTextBlock,
                        new TextBlock
                        {
                            Margin = new Thickness(0, 22, 0, 12),
                            Text = "Employee List",
                            FontSize = 18,
                            FontWeight = FontWeights.Bold,
                            Foreground = (Brush)FindResource("ThemeInkBrush")
                        },
                        employeeList,
                        employeeListStatusTextBlock
                    }
                }
            };
            Grid.SetColumn(employeeCard, 2);

            employeeNameTextBox.Visibility = AuthSessionStore.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
            employeeIdTextBox.Visibility = AuthSessionStore.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
            employeePinBox.Visibility = AuthSessionStore.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
            saveEmployeeButton.Visibility = AuthSessionStore.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
            employeeStatusTextBlock.Visibility = AuthSessionStore.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
            for (var index = 2; index <= 6; index += 2)
            {
                if (employeeCard.Child is StackPanel stackPanel)
                {
                    stackPanel.Children[index].Visibility = AuthSessionStore.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            var grid = new Grid
            {
                Margin = new Thickness(32)
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(accountCard);
            grid.Children.Add(employeeCard);
            _ = LoadEmployeesAsync();
            return grid;
        }

        private TextBox CreateSettingsTextBox()
        {
            return new TextBox
            {
                Height = 42,
                Width = 260,
                Padding = new Thickness(12, 0, 12, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = Brushes.White,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0)
            };
        }

        private TextBlock CreateSettingsLabel(string text)
        {
            return new TextBlock
            {
                Margin = new Thickness(0, 12, 0, 6),
                Text = text,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ThemeInkBrush")
            };
        }

        private Button CreateSmallSettingsButton(string text)
        {
            return new Button
            {
                Width = 78,
                Height = 30,
                Margin = new Thickness(0, 0, 8, 0),
                Background = (Brush)FindResource("ThemeAccentSoftBrush"),
                Foreground = (Brush)FindResource("ThemeInkBrush"),
                Content = text
            };
        }

        private void CommitCartQuantityEdit(TextBox? textBox)
        {
            if (textBox?.DataContext is not CartItem cartItem ||
                DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            if (!int.TryParse(textBox.Text, out var quantity))
            {
                textBox.Text = cartItem.Qty.ToString();
                return;
            }

            if (quantity <= 0)
            {
                viewModel.RemoveCartItem(cartItem);
                return;
            }

            if (quantity > 100)
            {
                quantity = 100;
            }

            viewModel.UpdateCartItemQuantity(cartItem, quantity);
            textBox.Text = quantity.ToString();
        }

        private void SetActiveNavButton(Button activeButton)
        {
            RegisterNavButton.Background = _inactiveNavBrush;
            OrdersNavButton.Background = _inactiveNavBrush;
            InventoryNavButton.Background = _inactiveNavBrush;
            CustomNavButton.Background = _inactiveNavBrush;
            activeButton.Background = _activeNavBrush;
        }

    }

}
