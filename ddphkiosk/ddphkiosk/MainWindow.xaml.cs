using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace ddphkiosk;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly FirebaseKioskService _service = new();
    private readonly List<ProductCard> _allProducts = [];
    private string _customerName = string.Empty;
    private string _customerPhone = string.Empty;
    private string _notes = string.Empty;
    private DateTime? _pickupDate = DateTime.Today;
    private string _pickupTime = "03:00 PM";
    private string _selectedCategory = string.Empty;
    private string _statusMessage = "Loading menu...";

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        LoadPickupTimes();
        AddToCartCommand = new RelayCommand<ProductCard>(AddToCart);
        DecreaseCartItemCommand = new RelayCommand<CartItem>(DecreaseCartItem);
        ClearCartCommand = new RelayCommand(ClearCart);
        SelectCategoryCommand = new RelayCommand<CategoryTab>(SelectCategory);
        SubmitOrderCommand = new AsyncRelayCommand(SubmitOrderAsync, HasCartItems);
        CloseAppCommand = new RelayCommand(CloseApp);
    }

    public ObservableCollection<ProductCard> Products { get; } = new();

    public ObservableCollection<ProductCard> FilteredProducts { get; } = new();

    public ObservableCollection<CartItem> CartItems { get; } = new();

    public ObservableCollection<CategoryTab> Categories { get; } = new();

    public ObservableCollection<string> PickupTimeOptions { get; } = new();

    public DateTime TodayDate => DateTime.Today;

    public ICommand AddToCartCommand { get; }

    public ICommand DecreaseCartItemCommand { get; }

    public ICommand ClearCartCommand { get; }

    public ICommand SelectCategoryCommand { get; }

    public ICommand SubmitOrderCommand { get; }

    public ICommand CloseAppCommand { get; }

    public string CustomerName
    {
        get => _customerName;
        set
        {
            if (SetProperty(ref _customerName, value))
            {
                RefreshSubmitState();
            }
        }
    }

    public string CustomerPhone
    {
        get => _customerPhone;
        set
        {
            if (SetProperty(ref _customerPhone, value))
            {
                RefreshSubmitState();
            }
        }
    }

    public DateTime? PickupDate
    {
        get => _pickupDate;
        set
        {
            if (SetProperty(ref _pickupDate, value))
            {
                RefreshPickupTimes();
                RefreshSubmitState();
            }
        }
    }

    public string PickupTime
    {
        get => _pickupTime;
        set
        {
            if (SetProperty(ref _pickupTime, value))
            {
                RefreshSubmitState();
            }
        }
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string CartSubtotalDisplay => CartItems.Sum(item => item.Total).ToPeso();

    public event PropertyChangedEventHandler? PropertyChanged;

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Maximized;
        MakeDatePickerPopupOnly(PickupDatePicker);
        await LoadMenuAsync();
    }

    private async Task LoadMenuAsync()
    {
        try
        {
            StatusMessage = "Loading menu...";
            var menu = await _service.GetMenuAsync();

            _allProducts.Clear();
            Products.Clear();
            FilteredProducts.Clear();
            foreach (var product in menu.Products)
            {
                _allProducts.Add(product);
                Products.Add(product);
            }

            Categories.Clear();
            foreach (var category in menu.Categories)
            {
                Categories.Add(category);
            }

            SelectCategory(Categories.FirstOrDefault());
            StatusMessage = Products.Count == 0 ? "No products found." : "Menu ready.";
            await LoadProductImagesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Load failed: {ex.Message}";
        }
    }

    private async Task LoadProductImagesAsync()
    {
        var imageTasks = _allProducts
            .Where(product => !string.IsNullOrWhiteSpace(product.Image))
            .Select(async product => (Product: product, ImageSource: await _service.GetProductImageAsync(product)))
            .ToList();

        if (imageTasks.Count == 0)
        {
            return;
        }

        StatusMessage = "Menu ready. Loading images...";

        while (imageTasks.Count > 0)
        {
            var finishedTask = await Task.WhenAny(imageTasks);
            imageTasks.Remove(finishedTask);

            var result = await finishedTask;
            result.Product.ProductImageSource = result.ImageSource;
        }

        StatusMessage = "Menu ready.";
    }

    private void AddToCart(ProductCard? product)
    {
        if (product is null)
        {
            return;
        }

        var existing = CartItems.FirstOrDefault(item => item.Product.Id == product.Id);
        if (existing is null)
        {
            var item = new CartItem(product);
            item.PropertyChanged += HandleCartChanged;
            CartItems.Add(item);
        }
        else
        {
            existing.Quantity++;
        }

        OnPropertyChanged(nameof(CartSubtotalDisplay));
        RefreshSubmitState();
    }

    private void DecreaseCartItem(CartItem? cartItem)
    {
        if (cartItem is null)
        {
            return;
        }

        if (cartItem.Quantity > 1)
        {
            cartItem.Quantity--;
        }
        else
        {
            cartItem.PropertyChanged -= HandleCartChanged;
            CartItems.Remove(cartItem);
        }

        OnPropertyChanged(nameof(CartSubtotalDisplay));
        RefreshSubmitState();
    }

    private void ClearCart()
    {
        foreach (var cartItem in CartItems)
        {
            cartItem.PropertyChanged -= HandleCartChanged;
        }

        CartItems.Clear();
        OnPropertyChanged(nameof(CartSubtotalDisplay));
        RefreshSubmitState();
    }

    private void SelectCategory(CategoryTab? category)
    {
        if (category is null || string.IsNullOrWhiteSpace(category.Name))
        {
            return;
        }

        _selectedCategory = category.Name;
        foreach (var tab in Categories)
        {
            tab.IsSelected = string.Equals(tab.Name, _selectedCategory, StringComparison.OrdinalIgnoreCase);
        }

        FilteredProducts.Clear();
        var filtered = string.Equals(category.Name, "All", StringComparison.OrdinalIgnoreCase)
            ? _allProducts
            : _allProducts.Where(product => string.Equals(product.Category, category.Name, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var product in filtered)
        {
            FilteredProducts.Add(product);
        }

        StatusMessage = FilteredProducts.Count == 0 ? $"No items in {category.Name}." : $"Showing {category.Name}.";
    }

    private async Task SubmitOrderAsync()
    {
        if (!HasRequiredCustomerDetails())
        {
            StatusMessage = "Fill name, phone, pickup date, and pickup time.";
            MessageBox.Show(
                "Fill name, phone, pickup date, and pickup time first.",
                "Missing Details",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!TryGetPickupDateTime(out var pickupDateTime))
        {
            StatusMessage = "Pickup date/time must be later than now.";
            MessageBox.Show(
                "Pickup date/time must be set later than the current time.",
                "Invalid Pickup Time",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var request = BuildOrderRequest();
        if (!ShowReceiptConfirmation(request))
        {
            StatusMessage = "Order confirmation cancelled.";
            return;
        }

        try
        {
            StatusMessage = "Sending order...";
            var orderId = await _service.CreateOrderAsync(request);
            StatusMessage = $"Order sent: {orderId}";
            ClearCart();
            ClearOrderInputs();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Order failed: {ex.Message}";
        }
    }

    private OrderCreateRequest BuildOrderRequest()
    {
        var items = CartItems.Select(item => new OrderItemDto
        {
            ProductId = item.Product.Id,
            Name = item.Product.Name,
            Category = item.Product.Category,
            Quantity = item.Quantity,
            Price = item.Product.Price,
            Subtotal = item.Total
        }).ToList();

        var subtotal = items.Sum(item => item.Subtotal);
        var now = DateTime.UtcNow;

        return new OrderCreateRequest
        {
            CustomerName = CustomerName.Trim(),
            CustomerPhone = CustomerPhone.Trim(),
            PickupDate = PickupDate?.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture) ?? string.Empty,
            PickupTime = PickupTime.Trim(),
            Notes = Notes.Trim(),
            Items = items,
            Subtotal = subtotal,
            Total = subtotal,
            Status = "pending",
            OrderNumber = Random.Shared.Next(1, 101),
            OrderType = "kiosk",
            PaymentStatus = "unpaid",
            CreatedAt = now.ToString("O", CultureInfo.InvariantCulture),
            UpdatedAt = now.ToString("O", CultureInfo.InvariantCulture),
            Date = DateTime.Now.ToString("MMM dd, yyyy, hh:mm tt", CultureInfo.InvariantCulture)
        };
    }

    private bool HasCartItems()
    {
        return CartItems.Count > 0;
    }

    private bool HasRequiredCustomerDetails()
    {
        return !string.IsNullOrWhiteSpace(CustomerName) &&
               !string.IsNullOrWhiteSpace(CustomerPhone) &&
               PickupDate.HasValue &&
               !string.IsNullOrWhiteSpace(PickupTime);
    }

    private bool TryGetPickupDateTime(out DateTime pickupDateTime)
    {
        pickupDateTime = default;

        if (!DateTime.TryParse(
                $"{PickupDate?.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture)} {PickupTime}",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out pickupDateTime))
        {
            return false;
        }

        return pickupDateTime > DateTime.Now;
    }

    private bool ShowReceiptConfirmation(OrderCreateRequest request)
    {
        try
        {
            var previewPages = ReceiptGenerator.CreatePreviewPages(request);
            var window = new ReceiptPreviewWindow(previewPages)
            {
                Owner = this
            };

            return window.ShowDialog() == true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Receipt failed: {ex.Message}";
            return false;
        }
    }

    private void ClearOrderInputs()
    {
        CustomerName = string.Empty;
        CustomerPhone = string.Empty;
        Notes = string.Empty;
        PickupDate = DateTime.Today;
        RefreshPickupTimes();
    }

    private void LoadPickupTimes()
    {
        RefreshPickupTimes();
    }

    private void RefreshPickupTimes()
    {
        PickupTimeOptions.Clear();
        var selectedDate = PickupDate?.Date ?? DateTime.Today;
        var now = DateTime.Now;

        for (var hour = 0; hour <= 23; hour++)
        {
            AddPickupTimeIfAllowed(selectedDate, now, hour, 0);
            AddPickupTimeIfAllowed(selectedDate, now, hour, 30);
        }

        if (!PickupTimeOptions.Contains(PickupTime))
        {
            PickupTime = PickupTimeOptions.FirstOrDefault() ?? string.Empty;
        }
    }

    private void AddPickupTimeIfAllowed(DateTime selectedDate, DateTime now, int hour, int minute)
    {
        var label = new DateTime(2000, 1, 1, hour, minute, 0).ToString("hh:mm tt", CultureInfo.InvariantCulture);
        if (selectedDate > now.Date)
        {
            PickupTimeOptions.Add(label);
            return;
        }

        var candidate = new DateTime(selectedDate.Year, selectedDate.Month, selectedDate.Day, hour, minute, 0);
        if (candidate > now)
        {
            PickupTimeOptions.Add(label);
        }
    }

    private static void MakeDatePickerPopupOnly(DatePicker? datePicker)
    {
        if (datePicker?.Template?.FindName("PART_TextBox", datePicker) is DatePickerTextBox textBox)
        {
            textBox.IsReadOnly = true;
            textBox.IsHitTestVisible = false;
            textBox.Focusable = false;
        }
    }

    private void CloseApp()
    {
        Application.Current.Shutdown();
    }

    private void HandleCartChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CartItem.Quantity) or nameof(CartItem.TotalDisplay))
        {
            OnPropertyChanged(nameof(CartSubtotalDisplay));
            RefreshSubmitState();
        }
    }

    private void RefreshSubmitState()
    {
        if (SubmitOrderCommand is AsyncRelayCommand asyncCommand)
        {
            asyncCommand.RaiseCanExecuteChanged();
        }
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class CategoryTab : INotifyPropertyChanged
{
    private bool _isSelected;

    public required string Key { get; init; }

    public required string Name { get; init; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class ProductCard : INotifyPropertyChanged
{
    private BitmapImage? _productImageSource;

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string ShortName { get; init; }

    public required string Category { get; init; }

    public required string Eyebrow { get; init; }

    public required string Description { get; init; }

    public required string Badge { get; init; }

    public required string Image { get; init; }

    public BitmapImage? ProductImageSource
    {
        get => _productImageSource;
        set
        {
            if (ReferenceEquals(_productImageSource, value))
            {
                return;
            }

            _productImageSource = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProductImageSource)));
        }
    }

    public required decimal Price { get; init; }

    public required Brush ArtBrush { get; init; }

    public string PriceDisplay => Price.ToPeso();

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class CartItem : INotifyPropertyChanged
{
    private int _quantity = 1;

    public CartItem(ProductCard product)
    {
        Product = product;
    }

    public ProductCard Product { get; }

    public int Quantity
    {
        get => _quantity;
        set
        {
            if (_quantity == value)
            {
                return;
            }

            _quantity = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(QuantityDisplay));
            OnPropertyChanged(nameof(Total));
            OnPropertyChanged(nameof(TotalDisplay));
        }
    }

    public decimal Total => Product.Price * Quantity;

    public string QuantityDisplay => $"Qty: {Quantity}";

    public string TotalDisplay => Total.ToPeso();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;

    public RelayCommand(Action<T?> execute)
    {
        _execute = execute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        _execute((T?)parameter);
    }
}

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;

    public RelayCommand(Action execute)
    {
        _execute = execute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        _execute();
    }
}

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isRunning;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter)
    {
        return !_isRunning && (_canExecute?.Invoke() ?? true);
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        try
        {
            _isRunning = true;
            RaiseCanExecuteChanged();
            await _execute();
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

internal static class CurrencyExtensions
{
    public static string ToPeso(this decimal value)
    {
        return $"PHP {value:N2}";
    }
}

public sealed class FirebaseProductsResponse : Dictionary<string, FirebaseProductDto?>
{
}

public sealed class FirebaseCategoriesResponse : Dictionary<string, FirebaseCategoryDto?>
{
}

public sealed class FirebaseProductDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }
}

public sealed class FirebaseCategoryDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("order")]
    public int? Order { get; set; }
}

public sealed class OrderCreateRequest
{
    [JsonPropertyName("customerName")]
    public string CustomerName { get; set; } = string.Empty;

    [JsonPropertyName("customerPhone")]
    public string CustomerPhone { get; set; } = string.Empty;

    [JsonPropertyName("pickupDate")]
    public string PickupDate { get; set; } = string.Empty;

    [JsonPropertyName("pickupTime")]
    public string PickupTime { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<OrderItemDto> Items { get; set; } = [];

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;

    [JsonPropertyName("subtotal")]
    public decimal Subtotal { get; set; }

    [JsonPropertyName("total")]
    public decimal Total { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("orderNumber")]
    public int OrderNumber { get; set; }

    [JsonPropertyName("orderType")]
    public string OrderType { get; set; } = "kiosk";

    [JsonPropertyName("paymentStatus")]
    public string PaymentStatus { get; set; } = "unpaid";

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    [JsonPropertyName("updatedAt")]
    public string UpdatedAt { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;
}

public sealed class OrderItemDto
{
    [JsonPropertyName("productId")]
    public string ProductId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("subtotal")]
    public decimal Subtotal { get; set; }
}
