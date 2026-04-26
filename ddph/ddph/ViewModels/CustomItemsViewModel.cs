using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using ddph.Data;
using ddph.Models;

namespace ddph.ViewModels
{
    public class CustomItemsViewModel : INotifyPropertyChanged
    {
        private readonly CustomItemRepository _customItemRepository = new();
        private readonly OrderRepository _orderRepository = new();
        private string _additionalNotes = string.Empty;
        private string _customerEmail = string.Empty;
        private string _customerName = string.Empty;
        private string _customerPhone = string.Empty;
        private string _deliveryAddress = string.Empty;
        private string _designDescription = string.Empty;
        private string _flavor = string.Empty;
        private string _pickupDate = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
        private string _pickupTime = "10:00";
        private string _productType = string.Empty;
        private int _quantity = 1;
        private string _referenceImageUrl = string.Empty;
        private string _searchText = string.Empty;
        private string _size = string.Empty;
        private bool _isLoading;

        public CustomItemsViewModel()
        {
            SampleCustomItems = new ObservableCollection<CustomItem>();
            ProductTypes = new ObservableCollection<string>
            {
                "Cake",
                "Cupcakes",
                "Cookies",
                "Other"
            };

            FilteredCustomItems = CollectionViewSource.GetDefaultView(SampleCustomItems);
            FilteredCustomItems.Filter = FilterCustomItems;

            SubmitCustomOrderCommand = new RelayCommand(async _ => await SubmitCustomOrderAsync(), _ => CanSubmitCustomOrder());
            RefreshCommand = new RelayCommand(async _ => await LoadSamplesAsync(), _ => !IsLoading);

            _ = LoadSamplesAsync();
        }

        public ObservableCollection<CustomItem> SampleCustomItems { get; }
        public ObservableCollection<string> ProductTypes { get; }
        public ICollectionView FilteredCustomItems { get; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value)
                {
                    return;
                }

                _searchText = value;
                OnPropertyChanged();
                FilteredCustomItems.Refresh();
            }
        }

        public string CustomerName
        {
            get => _customerName;
            set
            {
                if (_customerName == value)
                {
                    return;
                }

                _customerName = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string CustomerPhone
        {
            get => _customerPhone;
            set
            {
                if (_customerPhone == value)
                {
                    return;
                }

                _customerPhone = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string CustomerEmail
        {
            get => _customerEmail;
            set
            {
                if (_customerEmail == value)
                {
                    return;
                }

                _customerEmail = value;
                OnPropertyChanged();
            }
        }

        public string ProductType
        {
            get => _productType;
            set
            {
                if (_productType == value)
                {
                    return;
                }

                _productType = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

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
            }
        }

        public string Size
        {
            get => _size;
            set
            {
                if (_size == value)
                {
                    return;
                }

                _size = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string ReferenceImageUrl
        {
            get => _referenceImageUrl;
            set
            {
                if (_referenceImageUrl == value)
                {
                    return;
                }

                _referenceImageUrl = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PreviewImageUrl));
            }
        }

        public string Flavor
        {
            get => _flavor;
            set
            {
                if (_flavor == value)
                {
                    return;
                }

                _flavor = value;
                OnPropertyChanged();
            }
        }

        public string DesignDescription
        {
            get => _designDescription;
            set
            {
                if (_designDescription == value)
                {
                    return;
                }

                _designDescription = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string PickupDate
        {
            get => _pickupDate;
            set
            {
                if (_pickupDate == value)
                {
                    return;
                }

                _pickupDate = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string PickupTime
        {
            get => _pickupTime;
            set
            {
                if (_pickupTime == value)
                {
                    return;
                }

                _pickupTime = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string DeliveryAddress
        {
            get => _deliveryAddress;
            set
            {
                if (_deliveryAddress == value)
                {
                    return;
                }

                _deliveryAddress = value;
                OnPropertyChanged();
            }
        }

        public string AdditionalNotes
        {
            get => _additionalNotes;
            set
            {
                if (_additionalNotes == value)
                {
                    return;
                }

                _additionalNotes = value;
                OnPropertyChanged();
            }
        }

        public string PreviewImageUrl => ReferenceImageUrl;

        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (_isLoading == value)
                {
                    return;
                }

                _isLoading = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public ICommand SubmitCustomOrderCommand { get; }
        public ICommand RefreshCommand { get; }

        private bool FilterCustomItems(object item)
        {
            if (item is not CustomItem customItem)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return true;
            }

            return customItem.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                customItem.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }

        private bool CanSubmitCustomOrder()
        {
            return !string.IsNullOrWhiteSpace(CustomerName) &&
                !string.IsNullOrWhiteSpace(CustomerPhone) &&
                !string.IsNullOrWhiteSpace(ProductType) &&
                !string.IsNullOrWhiteSpace(Size) &&
                !string.IsNullOrWhiteSpace(DesignDescription) &&
                !string.IsNullOrWhiteSpace(PickupDate) &&
                !string.IsNullOrWhiteSpace(PickupTime) &&
                !IsLoading;
        }

        private async Task SubmitCustomOrderAsync()
        {
            try
            {
                IsLoading = true;
                var submission = new OrderRepository.CustomOrderSubmission
                {
                    AdditionalNotes = AdditionalNotes.Trim(),
                    CustomerEmail = CustomerEmail.Trim(),
                    CustomerName = CustomerName.Trim(),
                    CustomerPhone = CustomerPhone.Trim(),
                    DeliveryAddress = DeliveryAddress.Trim(),
                    DesignDescription = DesignDescription.Trim(),
                    Flavor = Flavor.Trim(),
                    PickupDate = PickupDate,
                    PickupTime = PickupTime,
                    ProductType = ProductType,
                    Quantity = Quantity,
                    ReferenceImageUrl = ReferenceImageUrl.Trim(),
                    Size = Size.Trim()
                };

                await _orderRepository.AddCustomOrderAsync(submission);
                MessageBox.Show(
                    "Custom order submitted to Firebase.",
                    "Custom Order",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to submit custom order.\n\n{ex.Message}",
                    "Custom Order Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadSamplesAsync()
        {
            try
            {
                IsLoading = true;
                SampleCustomItems.Clear();

                foreach (var item in await _customItemRepository.GetCustomItemsAsync())
                {
                    SampleCustomItems.Add(item);
                }

                FilteredCustomItems.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to load sample custom items.\n\n{ex.Message}",
                    "Custom Order Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ClearForm()
        {
            CustomerName = string.Empty;
            CustomerPhone = string.Empty;
            CustomerEmail = string.Empty;
            ProductType = string.Empty;
            Quantity = 1;
            Size = string.Empty;
            ReferenceImageUrl = string.Empty;
            Flavor = string.Empty;
            DesignDescription = string.Empty;
            PickupDate = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
            PickupTime = "10:00";
            DeliveryAddress = string.Empty;
            AdditionalNotes = string.Empty;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
