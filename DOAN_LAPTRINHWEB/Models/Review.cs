using System;
using System.Collections.Generic;

namespace DOAN_LAPTRINHWEB.Models;

public partial class Review
{
    public int ReviewId { get; set; }
    public int ProductId { get; set; }
    public string UserId { get; set; } 
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime? CreatedAt { get; set; }
    public virtual Product? Product { get; set; }
    public virtual ApplicationUser User { get; set; }
}