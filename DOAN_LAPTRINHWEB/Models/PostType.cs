using DOAN_LAPTRINHWEB.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DOAN_LAPTRINHWEB.Models
{
    public class PostType
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }

        public int? DisplayOrder { get; set; }

        [MaxLength(255)]
        public string? ImageUrl { get; set; }
        public virtual ICollection<Post> Posts { get; set; }
    }
}