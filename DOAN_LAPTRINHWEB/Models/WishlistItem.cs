using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DOAN_LAPTRINHWEB.Models; 

public class WishlistItem
{
    [Key]
    public int Id { get; set; } 

    [Required]
    public string UserId { get; set; } 

    [Required]
    public int ProductId { get; set; } 

    public DateTime AddedDate { get; set; } = DateTime.UtcNow; 

    [ForeignKey("ProductId")]
    public Product Product { get; set; }

    [ForeignKey("UserId")]
    public ApplicationUser User { get; set; } 
}