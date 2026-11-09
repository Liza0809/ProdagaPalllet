using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PalletApp.Models
{
    public class CartItem : INotifyPropertyChanged
    {
        public Product Product { get; set; }

        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (value < 1) value = 1;
                _quantity = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LineTotal));
                OnPropertyChanged(nameof(TotalVolume));

                if (QuantityForSplit > value)
                    QuantityForSplit = value;
            }
        }

        private int _quantityForSplit;
        public int QuantityForSplit
        {
            get => _quantityForSplit;
            set
            {
                if (value < 0) value = 0;
                if (value > Quantity) value = Quantity;
                _quantityForSplit = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SplitLineTotal));
                OnPropertyChanged(nameof(SplitVolume));
            }
        }

        public decimal LineTotal => Product.Price * Quantity;
        public decimal TotalVolume => Quantity;
        public decimal SplitLineTotal => Product.Price * QuantityForSplit;
        public decimal SplitVolume => QuantityForSplit;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}