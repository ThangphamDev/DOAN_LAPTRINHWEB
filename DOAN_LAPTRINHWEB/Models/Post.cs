using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DOAN_LAPTRINHWEB.Models;

namespace DOAN_LAPTRINHWEB.Models
{
    public class Post
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Title { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(MAX)")]
        public string Content { get; set; }

        [Required]
        public DateTime CreatedDate { get; set; }

        [ForeignKey("User")]
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }
        public string? ImageUrls { get; set; }
        [ForeignKey("PostType")]
        public int PostTypeId { get; set; }
        public PostType PostType { get; set; }

        [ForeignKey("ApprovedBy")]
        public string? ApprovedById { get; set; } 
        public ApplicationUser? ApprovedBy { get; set; }

        public DateTime? ApprovalDate { get; set; }

        [MaxLength(50)]
        public string ApprovalStatus { get; set; }

        [MaxLength(500)]
        public string? ApprovalComment { get; set; }

        [Required]
        public bool IsDeleted { get; set; } = false;

        public virtual ICollection<Comment> Comments { get; set; }
    }
}