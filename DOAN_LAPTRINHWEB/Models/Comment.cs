using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DOAN_LAPTRINHWEB.Models; // Đảm bảo namespace đúng

namespace DOAN_LAPTRINHWEB.Models
{
    public class Comment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [ForeignKey("Post")]
        public int PostId { get; set; }
        public Post Post { get; set; }

        [ForeignKey("User")]
        public string UserId { get; set; }
        public ApplicationUser User { get; set; } // Thay IdentityUser bằng ApplicationUser

        [Required]
        public string Content { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; }

        [ForeignKey("ParentComment")]
        public int? ParentCommentId { get; set; }
        public Comment ParentComment { get; set; }
    }
}