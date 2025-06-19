using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using DOAN_LAPTRINHWEB.Services;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using DOAN_LAPTRINHWEB.Models;
namespace DOAN_LAPTRINHWEB.Controllers
{
    [Authorize] 
    public class WishlistController : Controller 
    {
        private readonly IWishlistService _wishlistService;

        public WishlistController(IWishlistService wishlistService)
        {
            _wishlistService = wishlistService;
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }
       public class ProductDto
        {
            public int ProductId { get; set; }
            public string Name { get; set; }
            public decimal Price { get; set; }
            public string Description { get; set; }
            public string ImageUrl { get; set; } 
        }
        public async Task<IActionResult> Index()
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account", new { area = "Identity" });
            }

            var wishlistProducts = await _wishlistService.GetUserWishlistAsync(userId);

            var productDtos = wishlistProducts?.Select(p => new ProductDto
            {
                ProductId = p.ProductId,
                Name = p.Name,
                Price = p.Price,
                Description = p.Description,
                ImageUrl = p.ProductImages.FirstOrDefault(img => img.IsPrimary == true)?.ImageUrl
            }).ToList();
            return View(productDtos);
        }
        [HttpPost("api/wishlist/{productId}")] 
        public async Task<IActionResult> AddToWishlist(int productId)
        {
            var userId = GetUserId(); 
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "Bạn cần đăng nhập để thêm sản phẩm vào danh sách yêu thích." });
            }

            var success = await _wishlistService.AddToWishlistAsync(userId, productId);
            if (success)
            {
                return Ok(new { message = "Sản phẩm đã được thêm vào danh sách yêu thích!" });
            }
            else
            {
                return BadRequest(new { message = "Không thể thêm sản phẩm vào danh sách yêu thích (có thể đã tồn tại hoặc sản phẩm không hợp lệ)." });
            }
        }

        [HttpDelete("api/wishlist/{productId}")]
        public async Task<IActionResult> RemoveFromWishlist(int productId)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var success = await _wishlistService.RemoveFromWishlistAsync(userId, productId);

            if (success)
            {
                return Ok(new { message = "Product removed from wishlist successfully." });
            }
            else
            {
                return NotFound(new { message = "Product not found in wishlist." });
            }
        }

        [HttpGet("api/wishlist/check/{productId}")] 
        public async Task<IActionResult> CheckWishlistStatus(int productId)
        {
            var userId = GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }
            var isInWishlist = await _wishlistService.IsProductInWishlistAsync(userId, productId);
            return Ok(new { isInWishlist = isInWishlist });
        }
    }
}