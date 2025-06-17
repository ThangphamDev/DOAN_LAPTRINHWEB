using System.ComponentModel.DataAnnotations;

namespace DOAN_LAPTRINHWEB.Models
{
    public class CreatePostDto
    {
        [Required]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        public string? ImageUrls { get; set; }

        [Required]
        public int PostTypeId { get; set; }

    }
}
