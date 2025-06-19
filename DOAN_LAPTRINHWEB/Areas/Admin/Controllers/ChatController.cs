using Microsoft.AspNetCore.Mvc;
using DOAN_LAPTRINHWEB.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text;

namespace DOAN_LAPTRINHWEB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChatController : ControllerBase
    {
        private readonly HomeStylesDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public ChatController(
            HomeStylesDbContext context,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            HttpClient httpClient)
        {
            _context = context;
            _userManager = userManager;
            _configuration = configuration;
            _httpClient = httpClient;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendMessage([FromBody] ChatRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Message))
                {
                    return BadRequest(new ChatResponse
                    {
                        Success = false,
                        Message = "Tin nhắn không được để trống"
                    });
                }

                var sessionId = request.SessionId ?? Guid.NewGuid().ToString();
                var userId = User.Identity?.IsAuthenticated == true ? _userManager.GetUserId(User) : null;

                // Lưu tin nhắn của user
                var userMessage = new ChatMessage
                {
                    Message = request.Message,
                    Response = "",
                    SessionId = sessionId,
                    UserId = userId,
                    IsFromUser = true
                };
                _context.ChatMessages.Add(userMessage);
                await _context.SaveChangesAsync();

                // Lấy phản hồi từ Gemini AI
                var aiResponse = await GetGeminiResponseAsync(request.Message, userId);

                // Lưu phản hồi của AI
                var aiMessage = new ChatMessage
                {
                    Message = aiResponse,
                    Response = "",
                    SessionId = sessionId,
                    UserId = userId,
                    IsFromUser = false
                };
                _context.ChatMessages.Add(aiMessage);
                await _context.SaveChangesAsync();

                return Ok(new ChatResponse
                {
                    Success = true,
                    Message = request.Message,
                    Response = aiResponse,
                    SessionId = sessionId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ChatResponse
                {
                    Success = false,
                    Message = "Đã xảy ra lỗi khi xử lý tin nhắn: " + ex.Message
                });
            }
        }

        [HttpGet("history/{sessionId}")]
        public async Task<IActionResult> GetHistory(string sessionId)
        {
            try
            {
                var history = await _context.ChatMessages
                    .Where(m => m.SessionId == sessionId)
                    .OrderBy(m => m.CreatedAt)
                    .Take(20)
                    .Select(m => new
                    {
                        m.Id,
                        m.Message,
                        m.IsFromUser,
                        m.CreatedAt
                    })
                    .ToListAsync();

                return Ok(history);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Không thể lấy lịch sử chat: " + ex.Message);
            }
        }

        private async Task<string> GetGeminiResponseAsync(string message, string? userId = null)
        {
            try
            {
                var apiKey = _configuration["Gemini:ApiKey"];

                if (string.IsNullOrEmpty(apiKey))
                {
                    return GetDefaultResponse(message);
                }

                // Tạo context về cửa hàng từ database
                var storeContext = await GetStoreContextAsync();

                var systemPrompt = $@"Bạn là trợ lý AI thông minh cho website bán đồ trang trí nội thất HomeStyles. 
                
Thông tin cửa hàng:
{storeContext}

Hãy trả lời một cách thân thiện, chuyên nghiệp và hữu ích. Luôn tập trung vào:
- Tư vấn sản phẩm trang trí nội thất
- Hướng dẫn khách hàng
- Thông tin về giá cả, giao hàng
- Đưa ra gợi ý phù hợp

Trả lời bằng tiếng Việt, ngắn gọn nhưng đầy đủ thông tin.";

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = systemPrompt },
                                new { text = $"Khách hàng hỏi: {message}" }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        topK = 40,
                        topP = 0.95,
                        maxOutputTokens = 350
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-exp:generateContent?key={apiKey}",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

                    if (result.TryGetProperty("candidates", out var candidates) &&
                        candidates.GetArrayLength() > 0)
                    {
                        var candidate = candidates[0];
                        if (candidate.TryGetProperty("content", out var contentProp) &&
                            contentProp.TryGetProperty("parts", out var parts) &&
                            parts.GetArrayLength() > 0)
                        {
                            var part = parts[0];
                            if (part.TryGetProperty("text", out var textProp))
                            {
                                return textProp.GetString() ?? GetDefaultResponse(message);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Gemini API Error: {ex.Message}");
            }

            return GetDefaultResponse(message);
        }

        private async Task<string> GetStoreContextAsync()
        {
            try
            {
                // Lấy thông tin sản phẩm từ database
                var categories = await _context.Categories
                    .Select(c => c.Name)
                    .ToListAsync();

                var productCount = await _context.Products.CountAsync();

                var priceRange = await _context.Products
                    .Where(p => p.Price > 0)
                    .GroupBy(p => 1)
                    .Select(g => new { Min = g.Min(p => p.Price), Max = g.Max(p => p.Price) })
                    .FirstOrDefaultAsync();

                var context = $@"
- Cửa hàng chuyên bán đồ trang trí nội thất
- Có {productCount} sản phẩm
- Danh mục: {string.Join(", ", categories)}
- Giá từ {priceRange?.Min:N0}đ - {priceRange?.Max:N0}đ
- Giao hàng miễn phí đơn từ 500.000đ
- Thời gian giao hàng 1-3 ngày
- Chính sách đổi trả trong 30 ngày
- Website: HomeStyles";

                return context;
            }
            catch
            {
                return "Cửa hàng chuyên bán đồ trang trí nội thất với nhiều sản phẩm đa dạng.";
            }
        }

        private string GetDefaultResponse(string message)
        {
            var lowerMessage = message.ToLower();

            if (lowerMessage.Contains("chào") || lowerMessage.Contains("hello") || lowerMessage.Contains("hi"))
                return "Xin chào! Tôi là trợ lý AI của HomeStyles. Tôi có thể giúp bạn tìm hiểu về sản phẩm trang trí nội thất. Bạn cần hỗ trợ gì?";

            if (lowerMessage.Contains("sản phẩm") || lowerMessage.Contains("đồ trang trí"))
                return "Chúng tôi có nhiều sản phẩm trang trí đẹp như đèn, gối, tranh, đồng hồ. Bạn có thể xem chi tiết tại trang Sản phẩm.";

            if (lowerMessage.Contains("giá") || lowerMessage.Contains("tiền"))
                return "Giá sản phẩm của chúng tôi rất cạnh tranh, từ 100.000đ - 2.000.000đ. Bạn có thể xem chi tiết từng sản phẩm.";

            if (lowerMessage.Contains("giao hàng") || lowerMessage.Contains("ship"))
                return "Chúng tôi giao hàng miễn phí cho đơn từ 500.000đ trong nội thành. Thời gian giao hàng 1-3 ngày.";

            if (lowerMessage.Contains("đổi trả"))
                return "Chúng tôi có chính sách đổi trả trong vòng 30 ngày với điều kiện sản phẩm còn nguyên vẹn.";

            return "Cảm ơn bạn đã liên hệ! Tôi có thể giúp bạn tìm hiểu về sản phẩm trang trí, giá cả, và dịch vụ. Bạn có câu hỏi gì khác không?";
        }
    }
}
