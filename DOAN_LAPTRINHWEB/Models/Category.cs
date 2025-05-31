using System;
using System.Collections.Generic;

namespace DOAN_LAPTRINHWEB.Models;

public partial class Category
{
    public int CategoryId { get; set; }

    public string Name { get; set; }

    public string Type { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
