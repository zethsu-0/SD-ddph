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
using System.Windows.Controls.Primitives;
using System.Linq;
using System.IO;
using System.Globalization;
using ddph.Data;
using ddph.Models;
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
        private OnlineOrdersViewModel? _ordersViewModel;
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

        private async void OrdersButton_Click(object sender, RoutedEventArgs e)
        {
            await ShowOrdersTabAsync();
        }

        private void QuickViewButton_Click(object sender, RoutedEventArgs e)
        {
            var quickViewWindow = new QuickOrderViewWindow(DataContext as MainWindowViewModel)
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

            if (previewWindow.ShowDialog() != true)
            {
                return;
            }

            try
            {
                ReceiptPrintService.Print(receipt.PreviewImages, System.IO.Path.GetFileNameWithoutExtension(receipt.FilePath));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to print receipt.\n\n{ex.Message}",
                    "Print Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void PaymentTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                return;
            }

            var nextText = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength);
            nextText = nextText.Insert(textBox.CaretIndex, e.Text);
            e.Handled = !IsValidPaymentText(nextText);
        }

        private void PaymentTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (sender is not TextBox textBox ||
                !e.DataObject.GetDataPresent(DataFormats.Text) ||
                e.DataObject.GetData(DataFormats.Text) is not string pastedText)
            {
                e.CancelCommand();
                return;
            }

            var nextText = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength);
            nextText = nextText.Insert(textBox.CaretIndex, pastedText);

            if (!IsValidPaymentText(nextText))
            {
                e.CancelCommand();
            }
        }

        private static bool IsValidPaymentText(string text)
        {
            if (text == "0" || !decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out _))
            {
                return false;
            }

            return text.Count(char.IsDigit) <= MainWindowViewModel.MaxPaymentDigits;
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
            _ordersViewModel?.StopAutoRefresh();
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

        private async Task ShowOrdersTabAsync()
        {
            if (_ordersContent == null)
            {
                var ordersWindow = new OnlineOrders();
                _ordersContent = CreateEmbeddedWindowContent(ordersWindow);
                _ordersViewModel = ordersWindow.DataContext as OnlineOrdersViewModel;
            }

            PageTitleText.Text = "Orders";
            SetActiveNavButton(OrdersNavButton);
            ShowEmbeddedTab(_ordersContent);

            if (_ordersViewModel != null)
            {
                await _ordersViewModel.RefreshWhenOpenedAsync();
            }
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

            if (content != _ordersContent)
            {
                _ordersViewModel?.StopAutoRefresh();
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
            var orderRepository = new OrderRepository();
            var employeeList = new StackPanel();
            var salesRecordsList = new StackPanel();
            var salesPeriodButtons = new List<Button>();
            var salesSourceButtons = new List<Button>();
            var salesPeriod = SalesRecordPeriod.Daily;
            var salesSource = SalesRecordSource.All;
            var salesRecordsStatusTextBlock = new TextBlock
            {
                Margin = new Thickness(0, 10, 0, 0),
                Foreground = (Brush)FindResource("ThemeInkSoftBrush")
            };
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

            async Task LoadSalesRecordsAsync()
            {
                try
                {
                    salesRecordsList.Children.Clear();
                    salesRecordsStatusTextBlock.Text = "Loading records...";

                    var registerTask = orderRepository.GetRegisterOrdersAsync();
                    var onlineTask = orderRepository.GetOnlineOrdersAsync();

                    await Task.WhenAll(registerTask, onlineTask);

                    var orders = registerTask.Result
                        .Concat(onlineTask.Result)
                        .Where(order => order.Total > 0 && MatchesSalesSource(order, salesSource));

                    var rows = orders
                        .GroupBy(order => new
                        {
                            Period = GetSalesRecordPeriod(order.Date, salesPeriod),
                            Source = GetSalesSourceLabel(order)
                        })
                        .OrderByDescending(group => group.Key.Period.SortDate)
                        .ThenBy(group => group.Key.Source)
                        .Select(group => new
                        {
                            group.Key.Period.DisplayDate,
                            group.Key.Source,
                            Count = group.Count(),
                            Revenue = group.Sum(order => order.Total)
                        })
                        .Take(31)
                        .ToList();

                    foreach (var row in rows)
                    {
                        salesRecordsList.Children.Add(CreateSalesRecordRow(row.DisplayDate, row.Source, row.Count, row.Revenue));
                    }

                    salesRecordsStatusTextBlock.Text = rows.Count == 0 ? "No sales yet." : string.Empty;
                }
                catch (Exception ex)
                {
                    salesRecordsStatusTextBlock.Text = ex.Message;
                }
            }

            async Task SetSalesPeriodAsync(SalesRecordPeriod period)
            {
                salesPeriod = period;
                UpdateSalesPeriodButtons(salesPeriodButtons, salesPeriod);
                await LoadSalesRecordsAsync();
            }

            async Task SetSalesSourceAsync(SalesRecordSource source)
            {
                salesSource = source;
                UpdateSalesSourceButtons(salesSourceButtons, salesSource);
                await LoadSalesRecordsAsync();
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

            var closeAppButton = new Button
            {
                Width = 180,
                Height = 48,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 24, -28, -28),
                Background = (Brush)FindResource("ThemeAccentSoftBrush"),
                Foreground = (Brush)FindResource("ThemeAccentBrush"),
                Content = "Close App"
            };
            closeAppButton.Style = CreateCloseAppButtonStyle();
            closeAppButton.Click += CloseButton_Click;

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

            var refreshSalesButton = CreateSmallSettingsButton("Refresh");
            refreshSalesButton.Width = 92;
            refreshSalesButton.Click += async (_, _) => await LoadSalesRecordsAsync();
            var salesFilterPanel = new UniformGrid
            {
                Columns = 4,
                Rows = 1,
                Margin = new Thickness(0, 0, 0, 16)
            };

            AddSalesPeriodButton("Daily", SalesRecordPeriod.Daily);
            AddSalesPeriodButton("Weekly", SalesRecordPeriod.Weekly);
            AddSalesPeriodButton("Monthly", SalesRecordPeriod.Monthly);
            AddSalesPeriodButton("Annual", SalesRecordPeriod.Annual);

            void AddSalesPeriodButton(string label, SalesRecordPeriod period)
            {
                var button = CreateSmallSettingsButton(label);
                button.Width = double.NaN;
                button.Margin = new Thickness(0, 0, 8, 0);
                button.Tag = period;
                button.Click += async (_, _) => await SetSalesPeriodAsync(period);
                salesPeriodButtons.Add(button);
                salesFilterPanel.Children.Add(button);
            }

            UpdateSalesPeriodButtons(salesPeriodButtons, salesPeriod);
            var salesSourcePanel = new UniformGrid
            {
                Columns = 3,
                Rows = 1,
                Margin = new Thickness(0, 0, 0, 16)
            };

            AddSalesSourceButton("All", SalesRecordSource.All);
            AddSalesSourceButton("Walk-in", SalesRecordSource.WalkIn);
            AddSalesSourceButton("Online", SalesRecordSource.Online);

            void AddSalesSourceButton(string label, SalesRecordSource source)
            {
                var button = CreateSmallSettingsButton(label);
                button.Width = double.NaN;
                button.Margin = new Thickness(0, 0, 8, 0);
                button.Tag = source;
                button.Click += async (_, _) => await SetSalesSourceAsync(source);
                salesSourceButtons.Add(button);
                salesSourcePanel.Children.Add(button);
            }

            UpdateSalesSourceButtons(salesSourceButtons, salesSource);

            var salesCard = new Border
            {
                Width = 520,
                Padding = new Thickness(28),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Background = (Brush)FindResource("ThemeSurfaceBrush"),
                CornerRadius = new CornerRadius(24),
                Child = new StackPanel
                {
                    Children =
                    {
                        new Grid
                        {
                            ColumnDefinitions =
                            {
                                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                                new ColumnDefinition { Width = GridLength.Auto }
                            },
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = "Sales Records",
                                    FontSize = 24,
                                    FontWeight = FontWeights.Bold,
                                    Foreground = (Brush)FindResource("ThemeInkBrush")
                                },
                                refreshSalesButton
                            }
                        },
                        new TextBlock
                        {
                            Margin = new Thickness(0, 8, 0, 18),
                            Text = "Sales and revenue",
                            Foreground = (Brush)FindResource("ThemeInkSoftBrush")
                        },
                        salesFilterPanel,
                        salesSourcePanel,
                        CreateSalesRecordHeader(),
                        new ScrollViewer
                        {
                            MaxHeight = 360,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            Content = salesRecordsList
                        },
                        salesRecordsStatusTextBlock
                    }
                }
            };
            Grid.SetColumn(refreshSalesButton, 1);
            Grid.SetColumn(salesCard, 4);

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
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(closeAppButton, 2);
            Grid.SetColumn(closeAppButton, 5);
            grid.Children.Add(accountCard);
            grid.Children.Add(employeeCard);
            grid.Children.Add(salesCard);
            grid.Children.Add(closeAppButton);
            _ = LoadEmployeesAsync();
            _ = LoadSalesRecordsAsync();
            return grid;
        }

        private UIElement CreateSalesRecordHeader()
        {
            return new Grid
            {
                Margin = new Thickness(0, 0, 0, 8),
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(90) },
                    new ColumnDefinition { Width = new GridLength(70) },
                    new ColumnDefinition { Width = new GridLength(120) }
                },
                Children =
                {
                    CreateSalesHeaderText("Date", 0),
                    CreateSalesHeaderText("Type", 1),
                    CreateSalesHeaderText("Orders", 2),
                    CreateSalesHeaderText("Revenue", 3)
                }
            };
        }

        private UIElement CreateSalesRecordRow(string date, string source, int orderCount, decimal revenue)
        {
            var grid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 8),
                Background = Brushes.White,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(90) },
                    new ColumnDefinition { Width = new GridLength(70) },
                    new ColumnDefinition { Width = new GridLength(120) }
                }
            };

            grid.Children.Add(CreateSalesRowText(date, 0, HorizontalAlignment.Left));
            grid.Children.Add(CreateSalesRowText(source, 1, HorizontalAlignment.Left));
            grid.Children.Add(CreateSalesRowText(orderCount.ToString(CultureInfo.CurrentCulture), 2, HorizontalAlignment.Right));
            grid.Children.Add(CreateSalesRowText(revenue.ToString("C", CultureInfo.CurrentCulture), 3, HorizontalAlignment.Right));

            return new Border
            {
                Padding = new Thickness(12),
                Background = Brushes.White,
                CornerRadius = new CornerRadius(12),
                Child = grid
            };
        }

        private TextBlock CreateSalesHeaderText(string text, int column)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("ThemeInkSoftBrush")
            };
            Grid.SetColumn(textBlock, column);
            return textBlock;
        }

        private TextBlock CreateSalesRowText(string text, int column, HorizontalAlignment alignment)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                HorizontalAlignment = alignment,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("ThemeInkBrush")
            };
            Grid.SetColumn(textBlock, column);
            return textBlock;
        }

        private void UpdateSalesPeriodButtons(IEnumerable<Button> buttons, SalesRecordPeriod activePeriod)
        {
            foreach (var button in buttons)
            {
                var isActive = button.Tag is SalesRecordPeriod period && period == activePeriod;
                button.Background = isActive
                    ? (Brush)FindResource("ToastBrush")
                    : (Brush)FindResource("ThemeAccentSoftBrush");
                button.Foreground = isActive
                    ? Brushes.White
                    : (Brush)FindResource("ThemeInkBrush");
            }
        }

        private void UpdateSalesSourceButtons(IEnumerable<Button> buttons, SalesRecordSource activeSource)
        {
            foreach (var button in buttons)
            {
                var isActive = button.Tag is SalesRecordSource source && source == activeSource;
                button.Background = isActive
                    ? (Brush)FindResource("ToastBrush")
                    : (Brush)FindResource("ThemeAccentSoftBrush");
                button.Foreground = isActive
                    ? Brushes.White
                    : (Brush)FindResource("ThemeInkBrush");
            }
        }

        private static bool MatchesSalesSource(OnlineOrder order, SalesRecordSource source)
        {
            return source switch
            {
                SalesRecordSource.WalkIn => string.Equals(order.SourceLabel, "Register", StringComparison.OrdinalIgnoreCase),
                SalesRecordSource.Online => string.Equals(order.SourceLabel, "Online", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(order.SourceLabel, "Custom", StringComparison.OrdinalIgnoreCase),
                _ => true
            };
        }

        private static string GetSalesSourceLabel(OnlineOrder order)
        {
            return order.SourceLabel switch
            {
                "Register" => "Walk-in",
                "Custom" => "Online",
                _ => order.SourceLabel
            };
        }

        private static SalesRecordDate GetSalesRecordPeriod(string value, SalesRecordPeriod period)
        {
            if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var parsed) ||
                DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed))
            {
                var date = parsed.Date;
                return period switch
                {
                    SalesRecordPeriod.Weekly => GetWeeklySalesRecordDate(date),
                    SalesRecordPeriod.Monthly => new SalesRecordDate(
                        new DateTime(date.Year, date.Month, 1),
                        date.ToString("MMMM yyyy", CultureInfo.CurrentCulture)),
                    SalesRecordPeriod.Annual => new SalesRecordDate(
                        new DateTime(date.Year, 1, 1),
                        date.ToString("yyyy", CultureInfo.CurrentCulture)),
                    _ => new SalesRecordDate(date, date.ToString("MMM dd, yyyy", CultureInfo.CurrentCulture))
                };
            }

            return new SalesRecordDate(DateTime.MinValue, string.IsNullOrWhiteSpace(value) ? "Unknown" : value);
        }

        private static SalesRecordDate GetWeeklySalesRecordDate(DateTime date)
        {
            var calendar = CultureInfo.CurrentCulture.Calendar;
            var weekNumber = calendar.GetWeekOfYear(
                date,
                CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Monday);
            var startOffset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var startDate = date.AddDays(-startOffset);
            var endDate = startDate.AddDays(6);

            return new SalesRecordDate(
                startDate,
                $"Week {weekNumber}: {startDate:MMM dd} - {endDate:MMM dd}");
        }

        private enum SalesRecordPeriod
        {
            Daily,
            Weekly,
            Monthly,
            Annual
        }

        private enum SalesRecordSource
        {
            All,
            WalkIn,
            Online
        }

        private sealed record SalesRecordDate(DateTime SortDate, string DisplayDate);

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

        private Style CreateCloseAppButtonStyle()
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.BackgroundProperty, (Brush)FindResource("ThemeAccentSoftBrush")));
            style.Setters.Add(new Setter(Control.ForegroundProperty, (Brush)FindResource("ThemeAccentBrush")));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));

            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "Root";
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
            borderFactory.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
            borderFactory.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
            borderFactory.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);
            template.VisualTree = borderFactory;

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Red));
            hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
            template.Triggers.Add(hoverTrigger);

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
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
