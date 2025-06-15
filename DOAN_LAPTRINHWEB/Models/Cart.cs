using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;

namespace DOAN_LAPTRINHWEB.Models
{
    public class Cart
    {
        public List<CartItem> Items { get; set; } = new List<CartItem>();

        public decimal SubTotal => Items.Sum(item => item.Subtotal);
        public decimal TotalAmount { get; set; }
        public decimal Shipping { get; set; } = 0;
        public decimal Tax { get; set; } = 0;

        public void CalculateTotals()
        {
            // Update each item's subtotal
            foreach (var item in Items)
            {
                item.UpdateSubtotal();
            }

            // Calculate final total
            TotalAmount = SubTotal + Shipping + Tax;
        }

        public int TotalItems => Items.Sum(i => i.Quantity);
    }

    public class CartItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Subtotal { get; set; }
        public string ImageUrl { get; set; }

        public void UpdateSubtotal()
        {
            Subtotal = Price * Quantity;
        }
    }

    public class CheckoutViewModel
    {
        public CheckoutViewModel()
        {
            // Initialize all required properties to prevent null reference exceptions
            Cart = new Cart();
            Address = new Address();
            Order = new Order();
        }

        public Cart Cart { get; set; }

        [Required(ErrorMessage = "Địa chỉ giao hàng là bắt buộc")]
        public Address Address { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán")]
        public string PaymentMethod { get; set; }

        public Order Order { get; set; }

        public ApplicationUser User { get; set; }

        public string UserId { get; set; }

        public bool SaveAddressToAccount { get; set; } = false;
    }

    public class OrderConfirmationViewModel
    {
        public Order Order { get; set; }
        public Address Address { get; set; }
        public Payment Payment { get; set; }
        public List<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}