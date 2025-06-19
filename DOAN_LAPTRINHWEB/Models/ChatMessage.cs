using System.ComponentModel.DataAnnotations;

namespace DOAN_LAPTRINHWEB.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }

        [Required]
        public string Message { get; set; }

        public string Response { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? UserId { get; set; }

        public string SessionId { get; set; }

        public bool IsFromUser { get; set; } = true;

        // Navigation property
        public virtual ApplicationUser? User { get; set; }
    }

    public class ChatRequest
    {
        [Required]
        public string Message { get; set; }

        public string? SessionId { get; set; }
    }

    public class ChatResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string Response { get; set; }
        public string SessionId { get; set; }
    }
}
