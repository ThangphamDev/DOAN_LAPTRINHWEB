using DOAN_LAPTRINHWEB.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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

                // Phân tích nội dung tin nhắn để xác định yêu cầu về sản phẩm
                var productInfo = await GetProductInfoForQuery(request.Message);

                // Lấy phản hồi từ Gemini AI
                var aiResponse = await GetGeminiResponseAsync(request.Message, userId, productInfo);

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

        private async Task<string> GetProductInfoForQuery(string query)
        {
            try
            {
                // Xử lý query để xác định loại thông tin người dùng đang tìm kiếm
                query = query.ToLower();
                var productInfoBuilder = new StringBuilder();

                // Tìm kiếm dựa trên danh mục
                if (query.Contains("danh mục") || query.Contains("loại") || query.Contains("thể loại"))
                {
                    // Tìm các từ khóa liên quan đến danh mục trong truy vấn
                    var categoryKeywords = await _context.Categories
                        .Select(c => c.Name.ToLower())
                        .ToListAsync();

                    foreach (var keyword in categoryKeywords)
                    {
                        if (query.Contains(keyword))
                        {
                            var products = await _context.Products
                                .Include(p => p.Category)
                                .Include(p => p.ProductImages)
                                .Where(p => p.Category.Name.ToLower().Contains(keyword))
                                .Take(5) // Giới hạn số lượng sản phẩm trả về
                                .ToListAsync();

                            if (products.Any())
                            {
                                productInfoBuilder.AppendLine($"Sản phẩm thuộc danh mục '{CultureInfo.CurrentCulture.TextInfo.ToTitleCase(keyword)}':");
                                foreach (var product in products)
                                {
                                    productInfoBuilder.AppendLine($"- {product.Name}: {product.Price:N0}đ");
                                    if (!string.IsNullOrEmpty(product.Description))
                                    {
                                        var shortDesc = product.Description.Length > 100
                                            ? product.Description.Substring(0, 100) + "..."
                                            : product.Description;
                                        productInfoBuilder.AppendLine($"  Mô tả: {shortDesc}");
                                    }
                                }
                            }
                        }
                    }
                }
                // Tìm kiếm dựa trên tên sản phẩm cụ thể
                else if (query.Contains("sản phẩm") || query.Contains("đồ"))
                {
                    // Trích xuất từ khóa tìm kiếm
                    var searchTerms = ExtractSearchTerms(query);

                    if (searchTerms.Any())
                    {
                        var products = new List<Product>();

                        foreach (var term in searchTerms)
                        {
                            var matchingProducts = await _context.Products
                                .Include(p => p.Category)
                                .Include(p => p.ProductImages)
                                .Include(p => p.Tags)
                                .Where(p => p.Name.ToLower().Contains(term) ||
                                            p.Description.ToLower().Contains(term) ||
                                            p.Tags.Any(t => t.Name.ToLower().Contains(term)))
                                .Take(3) // Lấy tối đa 3 sản phẩm cho mỗi từ khóa
                                .ToListAsync();

                            products.AddRange(matchingProducts);
                        }

                        // Loại bỏ trùng lặp và giới hạn số lượng
                        products = products.DistinctBy(p => p.ProductId).Take(5).ToList();

                        if (products.Any())
                        {
                            productInfoBuilder.AppendLine("Sản phẩm liên quan:");
                            foreach (var product in products)
                            {
                                productInfoBuilder.AppendLine($"- {product.Name} ({product.Category?.Name}): {product.Price:N0}đ");
                                if (!string.IsNullOrEmpty(product.Description))
                                {
                                    var shortDesc = product.Description.Length > 100
                                        ? product.Description.Substring(0, 100) + "..."
                                        : product.Description;
                                    productInfoBuilder.AppendLine($"  Mô tả: {shortDesc}");
                                }
                            }
                        }
                    }
                }
                // Tìm kiếm dựa trên khoảng giá
                else if (query.Contains("giá") || query.Contains("tiền"))
                {
                    // Trích xuất khoảng giá từ tin nhắn
                    var priceRange = ExtractPriceRange(query);
                    if (priceRange != null)
                    {
                        var minPrice = priceRange.Item1;
                        var maxPrice = priceRange.Item2;

                        var products = await _context.Products
                            .Include(p => p.Category)
                            .Where(p => p.Price >= minPrice && p.Price <= maxPrice)
                            .OrderBy(p => p.Price)
                            .Take(5)
                            .ToListAsync();

                        if (products.Any())
                        {
                            productInfoBuilder.AppendLine($"Sản phẩm có giá từ {minPrice:N0}đ đến {maxPrice:N0}đ:");
                            foreach (var product in products)
                            {
                                productInfoBuilder.AppendLine($"- {product.Name} ({product.Category?.Name}): {product.Price:N0}đ");
                            }
                        }
                    }
                    else
                    {
                        // Trả về thông tin chung về phân khúc giá
                        var products = await _context.Products
                            .Select(p => p.Price)
                            .ToListAsync();

                        // Sau đó thực hiện phân nhóm trong bộ nhớ
                        var priceGroups = products
                            .GroupBy(price => {
                                if (price < 200000) return "Dưới 200.000đ";
                                else if (price >= 200000 && price < 500000) return "200.000đ - 500.000đ";
                                else if (price >= 500000 && price < 1000000) return "500.000đ - 1.000.000đ";
                                else return "Trên 1.000.000đ";
                            })
                            .Select(g => new { PriceRange = g.Key, Count = g.Count() })
                            .OrderBy(g => g.PriceRange.StartsWith("Dưới") ? 1 :
                                        g.PriceRange.StartsWith("200") ? 2 :
                                        g.PriceRange.StartsWith("500") ? 3 : 4)
                            .ToList();

                        productInfoBuilder.AppendLine("Thông tin phân khúc giá sản phẩm:");
                        foreach (var group in priceGroups.OrderBy(g => g.PriceRange))
                        {
                            productInfoBuilder.AppendLine($"- {group.PriceRange}: {group.Count} sản phẩm");
                        }
                    }
                }
                // Trả về sản phẩm nổi bật hoặc mới khi không có từ khóa tìm kiếm cụ thể
                else
                {
                    var featuredProducts = await _context.Products
                        .Include(p => p.Category)
                        .OrderByDescending(p => p.CreatedAt)
                        .Take(5)
                        .ToListAsync();

                    if (featuredProducts.Any())
                    {
                        productInfoBuilder.AppendLine("Sản phẩm mới nhất:");
                        foreach (var product in featuredProducts)
                        {
                            productInfoBuilder.AppendLine($"- {product.Name} ({product.Category?.Name}): {product.Price:N0}đ");
                        }
                    }
                }

                return productInfoBuilder.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting product info: {ex.Message}");
                return string.Empty;
            }
        }

        private List<string> ExtractSearchTerms(string query)
        {
            // Các từ khóa không liên quan đến tên sản phẩm
            var stopWords = new[] { "sản phẩm", "đồ", "về", "có", "không", "của", "cho" };

            // Loại bỏ dấu câu và chia câu thành từng từ
            var words = Regex.Replace(query.ToLower(), @"[^\p{L}\d\s]", "")
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !stopWords.Contains(w))
                .ToList();

            return words;
        }

        private Tuple<decimal, decimal> ExtractPriceRange(string query)
        {
            try
            {
                // Tìm các con số trong tin nhắn
                var numbers = Regex.Matches(query, @"\d+(?:\.\d+)?")
                    .Select(m => decimal.Parse(m.Value))
                    .ToList();

                if (numbers.Count >= 2)
                {
                    // Sắp xếp tăng dần
                    numbers.Sort();
                    // Giả định 2 số đầu tiên là min và max
                    return new Tuple<decimal, decimal>(numbers[0], numbers[1]);
                }
                else if (numbers.Count == 1)
                {
                    // Nếu chỉ có 1 số, kiểm tra xem là giá tối thiểu hay tối đa
                    if (query.Contains("dưới") || query.Contains("ít hơn") || query.Contains("nhỏ hơn"))
                    {
                        return new Tuple<decimal, decimal>(0, numbers[0]);
                    }
                    else if (query.Contains("trên") || query.Contains("hơn") || query.Contains("lớn hơn"))
                    {
                        return new Tuple<decimal, decimal>(numbers[0], decimal.MaxValue);
                    }
                    else
                    {
                        // Nếu không rõ, giả định là khoảng giá xung quanh số đó
                        var buffer = numbers[0] * 0.2m; // Buffer 20%
                        return new Tuple<decimal, decimal>(numbers[0] - buffer, numbers[0] + buffer);
                    }
                }
            }
            catch
            {
                // Bỏ qua lỗi phân tích
            }

            return null;
        }

        private async Task<string> GetGeminiResponseAsync(string message, string? userId = null, string productInfo = null)
        {
            try
            {
                var apiKey = _configuration["Gemini:ApiKey"];

                if (string.IsNullOrEmpty(apiKey))
                {
                    return GetDefaultResponse(message, productInfo);
                }

                // Tạo context về cửa hàng từ database
                var storeContext = await GetStoreContextAsync();

                // Kết hợp thông tin cửa hàng và thông tin sản phẩm cụ thể
                var fullContext = storeContext;
                if (!string.IsNullOrEmpty(productInfo))
                {
                    fullContext += "\n\nThông tin sản phẩm chi tiết:\n" + productInfo;
                }

                var systemPrompt = $@"Bạn là trợ lý AI thông minh cho website bán đồ trang trí nội thất HomeStyles. 
                
Thông tin cửa hàng và sản phẩm:
{fullContext}

Hãy trả lời một cách thân thiện, chuyên nghiệp và hữu ích. Luôn tập trung vào:
- Tư vấn sản phẩm trang trí nội thất
- Hướng dẫn khách hàng
- Thông tin về giá cả, giao hàng
- Đưa ra gợi ý phù hợp dựa vào thông tin sản phẩm được cung cấp

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
                        maxOutputTokens = 500 // Tăng số token để có câu trả lời chi tiết hơn
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
                                return textProp.GetString() ?? GetDefaultResponse(message, productInfo);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Gemini API Error: {ex.Message}");
            }

            return GetDefaultResponse(message, productInfo);
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

                // Thêm thông tin về số lượng sản phẩm trong mỗi danh mục
                var categoryStats = await _context.Categories
                    .Select(c => new { c.Name, ProductCount = c.Products.Count })
                    .Where(c => c.ProductCount > 0)
                    .OrderByDescending(c => c.ProductCount)
                    .ToListAsync();

                var categoryDetails = string.Join("\n", categoryStats.Select(c => $"  - {c.Name}: {c.ProductCount} sản phẩm"));

                // Thêm thông tin về các tag phổ biến
                var popularTags = await _context.Tags
                    .OrderByDescending(t => t.Products.Count)
                    .Take(10)
                    .Select(t => t.Name)
                    .ToListAsync();

                var context = $@"
- Cửa hàng chuyên bán đồ trang trí nội thất
- Có {productCount} sản phẩm
- Danh mục: {string.Join(", ", categories)}
- Thống kê danh mục:
{categoryDetails}
- Từ khóa phổ biến: {string.Join(", ", popularTags)}
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

        private string GetDefaultResponse(string message, string productInfo = null)
        {
            var lowerMessage = message.ToLower();

            // Nếu có thông tin sản phẩm liên quan, trả về kèm theo
            if (!string.IsNullOrEmpty(productInfo))
            {
                return $"Dựa trên thông tin từ cửa hàng của chúng tôi:\n\n{productInfo}\n\nBạn cần tìm hiểu thêm thông tin gì không?";
            }

            if (lowerMessage.Contains("chào") || lowerMessage.Contains("hello") || lowerMessage.Contains("hi"))
                return "Xin chào! Tôi là trợ lý AI của HomeStyles. Tôi có thể giúp bạn tìm hiểu về sản phẩm trang trí nội thất. Bạn cần hỗ trợ gì?";

            if (lowerMessage.Contains("sản phẩm") || lowerMessage.Contains("đồ trang trí"))
                return "Chúng tôi có nhiều sản phẩm trang trí đẹp như đèn, gối, tranh, đồng hồ. Bạn có thể nói rõ hơn về loại sản phẩm bạn quan tâm không?";

            if (lowerMessage.Contains("giá") || lowerMessage.Contains("tiền"))
                return "Giá sản phẩm của chúng tôi rất cạnh tranh, từ 100.000đ - 2.000.000đ. Bạn có thể cho biết khoảng giá hoặc loại sản phẩm bạn đang tìm kiếm không?";

            if (lowerMessage.Contains("giao hàng") || lowerMessage.Contains("ship"))
                return "Chúng tôi giao hàng miễn phí cho đơn từ 500.000đ trong nội thành. Thời gian giao hàng 1-3 ngày.";

            if (lowerMessage.Contains("đổi trả"))
                return "Chúng tôi có chính sách đổi trả trong vòng 30 ngày với điều kiện sản phẩm còn nguyên vẹn.";

            return "Cảm ơn bạn đã liên hệ! Tôi có thể giúp bạn tìm hiểu về sản phẩm trang trí, giá cả, và dịch vụ. Bạn có câu hỏi gì khác không?";
        }
    }
}