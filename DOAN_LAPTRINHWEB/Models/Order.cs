using System;
using System.Collections.Generic;

namespace DOAN_LAPTRINHWEB.Models;

public partial class Order
{
    public int OrderId { get; set; }
    public string UserId { get; set; } 
    public string OrderStatus { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ApplicationUser User { get; set; }
}