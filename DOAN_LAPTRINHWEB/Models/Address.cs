using System;
using System.Collections.Generic;

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

    public virtual ApplicationUser? User { get; set; }
}