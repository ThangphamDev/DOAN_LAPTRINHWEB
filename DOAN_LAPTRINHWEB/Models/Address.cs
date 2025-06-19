using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DOAN_LAPTRINHWEB.Models;

public partial class Address
{
    public int AddressId { get; set; }

    public string UserId { get; set; }

    public string FullName { get; set; }

    public string Phone { get; set; }

    public string Street { get; set; }

    public string City { get; set; }

    public string State { get; set; }

    public string PostalCode { get; set; }

    public string Country { get; set; }

    public bool IsDefault { get; set; }

    // **THÊM MỚI** - Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public virtual ApplicationUser User { get; set; }

    // **THÊM MỚI** - Navigation property đến Orders
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
