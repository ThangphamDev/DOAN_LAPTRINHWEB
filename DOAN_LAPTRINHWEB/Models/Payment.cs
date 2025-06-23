using System;
using System.Collections.Generic;

namespace DOAN_LAPTRINHWEB.Models;

public partial class Payment
{
    public int PaymentId { get; set; }

    public int OrderId { get; set; }

    public string PaymentMethod { get; set; }

    public string PaymentStatus { get; set; }

    public decimal Amount { get; set; }

    public DateTime PaidAt { get; set; }

    // Add this property if not already existing
    public string? TransactionId { get; set; }
    public string? PaymentDetails { get; set; }
    public virtual Order? Order { get; set; }
}
