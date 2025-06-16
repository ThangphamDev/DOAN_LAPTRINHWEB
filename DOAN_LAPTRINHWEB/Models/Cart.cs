using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DOAN_LAPTRINHWEB.Models
{
    public class Cart
    {
        public int CartId { get; set; }
        public string UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public virtual ApplicationUser User { get; set; }
        public virtual List<CartItem> Items { get; set; } = new List<CartItem>();

        [NotMapped]
        public decimal SubTotal => Items.Sum(item => item.Subtotal);

        [NotMapped]
        public decimal TotalAmount
        {
            get => SubTotal + Shipping + Tax;
            set { /* Giữ property setter cho tương thích với code hiện tại */ }
        }

        [NotMapped]
        public decimal Shipping { get; set; } = 0;

        [NotMapped]
        public decimal Tax { get; set; } = 0;

        [NotMapped]
        public int TotalItems => Items.Sum(i => i.Quantity);

        [NotMapped]
        public int UniqueItemsCount => Items.Count;
        public void CalculateTotals()
        {
            // Update each item's subtotal
            foreach (var item in Items)
            {
                item.UpdateSubtotal();
            }

            // UpdatedAt được cập nhật mỗi khi tính toán lại
            UpdatedAt = DateTime.Now;
        }
    }

    public class CartItem
    {
        public int CartItemId { get; set; }
        public int CartId { get; set; }
        public int ProductId { get; set; }
        public int? ProductVariantId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }
        public string ImageUrl { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.Now;

        public virtual Cart Cart { get; set; }
        public virtual Product Product { get; set; }
        public virtual ProductVariant ProductVariant { get; set; }

        public void UpdateSubtotal()
        {
            Subtotal = Price * Quantity;
        }
    }

   
    public class CheckoutViewModel
    {
        public CheckoutViewModel()
        {
            
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