using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace DOAN_LAPTRINHWEB.Models 
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        public string FullName { get; set; } 

        public string? Address { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Gender { get; set; }
        public string? AvatarUrl { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public virtual ICollection<Address> Addresses { get; set; } = new List<Address>();
        public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

        public ApplicationUser()
        {
            FullName = string.Empty; 
            CreatedAt = DateTime.UtcNow;
            AvatarUrl = "/images/default-avatar.png";
        }
    }
}