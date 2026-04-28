using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ddph.Models
{
    public class OnlineOrder : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _customerName = string.Empty;
        private string _customerPhone = string.Empty;
        private string _customerEmail = string.Empty;
        private string _status = string.Empty;
        private string _paymentStatus = string.Empty;
        private string _orderSource = string.Empty;
        private string _orderType = string.Empty;
        private string _orderNumber = string.Empty;
        private string _pickupDate = string.Empty;
        private string _pickupTime = string.Empty;
        private string _notes = string.Empty;
        private decimal _subtotal;
        private decimal _total;
        private decimal _payment;
        private decimal _change;
        private string _date = string.Empty;

        public OnlineOrder()
        {
            Items = new ObservableCollection<OnlineOrderItem>();
        }

        public string Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged();
            }
        }

        public string CustomerName
        {
            get => _customerName;
            set
            {
                _customerName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public string CustomerPhone
        {
            get => _customerPhone;
            set
            {
                _customerPhone = value;
                OnPropertyChanged();
            }
        }

        public string CustomerEmail
        {
            get => _customerEmail;
            set
            {
                _customerEmail = value;
                OnPropertyChanged();
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusBackground));
                OnPropertyChanged(nameof(StatusForeground));
            }
        }

        public string PaymentStatus
        {
            get => _paymentStatus;
            set
            {
                _paymentStatus = value;
                OnPropertyChanged();
            }
        }

        public string OrderSource
        {
            get => _orderSource;
            set
            {
                _orderSource = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SourceLabel));
            }
        }

        public string OrderType
        {
            get => _orderType;
            set
            {
                _orderType = value;
                OnPropertyChanged();
            }
        }

        public string OrderNumber
        {
            get => _orderNumber;
            set
            {
                _orderNumber = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReferenceLabel));
            }
        }

        public string PickupDate
        {
            get => _pickupDate;
            set
            {
                _pickupDate = value;
                OnPropertyChanged();
            }
        }

        public string PickupTime
        {
            get => _pickupTime;
            set
            {
                _pickupTime = value;
                OnPropertyChanged();
            }
        }

        public string Notes
        {
            get => _notes;
            set
            {
                _notes = value;
                OnPropertyChanged();
            }
        }

        public decimal Subtotal
        {
            get => _subtotal;
            set
            {
                _subtotal = value;
                OnPropertyChanged();
            }
        }

        public decimal Total
        {
            get => _total;
            set
            {
                _total = value;
                OnPropertyChanged();
            }
        }

        public decimal Payment
        {
            get => _payment;
            set
            {
                _payment = value;
                OnPropertyChanged();
            }
        }

        public decimal Change
        {
            get => _change;
            set
            {
                _change = value;
                OnPropertyChanged();
            }
        }

        public string Date
        {
            get => _date;
            set
            {
                _date = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<OnlineOrderItem> Items { get; }

        public string DisplayName => string.IsNullOrWhiteSpace(CustomerName) ? "Walk-in Customer" : CustomerName;
        public string SourceLabel => string.IsNullOrWhiteSpace(OrderSource) ? "Online" : OrderSource;
        public string ReferenceLabel => string.IsNullOrWhiteSpace(OrderNumber) ? Id : OrderNumber;
        public string StatusBackground => Status.ToLowerInvariant() switch
        {
            "confirmed" => "#EAF6ED",
            "preparing" => "#FFF2D7",
            "completed" => "#E9F8F5",
            "cancelled" => "#FCE7E7",
            "adjustment" => "#F4EAFE",
            _ => "#EFE7D9"
        };
        public string StatusForeground => Status.ToLowerInvariant() switch
        {
            "confirmed" => "#1F6B35",
            "preparing" => "#8A5A00",
            "completed" => "#0F766E",
            "cancelled" => "#B42318",
            "adjustment" => "#6B3FA0",
            _ => "#765449"
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
