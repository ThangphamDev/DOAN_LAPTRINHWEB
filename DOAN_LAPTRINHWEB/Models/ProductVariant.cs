using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DOAN_LAPTRINHWEB.Models;

public partial class ProductVariant
{
    public int VariantId { get; set; }

    public int ProductId { get; set; }

    [Required(ErrorMessage = "Kích thước là bắt buộc")]
    public string Size { get; set; }

    [Required(ErrorMessage = "Màu sắc là bắt buộc")]
    public string Color { get; set; }

    [Required(ErrorMessage = "Số lượng tồn kho là bắt buộc")]
    public int Stock { get; set; }

    [Required(ErrorMessage = "Giá bán là bắt buộc")]
    public decimal? AdditionalPrice { get; set; }

    public string? SKU { get; set; }

    public virtual Product Product { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}