using System;
using System.Collections.Generic;
using System.Text;

namespace ddph
{
    public class CartItem
    {
        public string ProductId { get; set; } = string.Empty;
        public string Item { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Qty { get; set; }
        public decimal Price { get; set; }
        public decimal LineTotal => Price * Qty;
    }
}
