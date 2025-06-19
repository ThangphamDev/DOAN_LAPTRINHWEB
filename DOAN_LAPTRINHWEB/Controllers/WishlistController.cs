using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DOAN_LAPTRINHWEB.Models;
using System.Security.Claims;

namespace DOAN_LAPTRINHWEB.Controllers
{
    [Authorize]
    public class WishlistController : Controller
    {
        private readonly HomeStylesDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public WishlistController(HomeStylesDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Hiển thị danh sách yêu thích
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var wishlistItems = await _context.Wishlists
                .Include(w => w.Product)
                    .ThenInclude(p => p.ProductImages)
                .Include(w => w.Product)
                    .ThenInclude(p => p.Category)
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync();

            return View(wishlistItems);
        }

        // Thêm sản phẩm vào yêu thích
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToWishlist(int productId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Kiểm tra sản phẩm có tồn tại không
                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                {
                    return Json(new { success = false, message = "Sản phẩm không tồn tại." });
                }

                // Kiểm tra đã có trong wishlist chưa
                var existingItem = await _context.Wishlists
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId);

                if (existingItem != null)
                {
                    return Json(new { success = false, message = "Sản phẩm đã có trong danh sách yêu thích." });
                }

                // Thêm vào wishlist
                var wishlistItem = new Wishlist
                {
                    UserId = userId,
                    ProductId = productId,
                    CreatedAt = DateTime.Now
                };

                _context.Wishlists.Add(wishlistItem);
                await _context.SaveChangesAsync();

                // Đếm số lượng items trong wishlist
                var wishlistCount = await _context.Wishlists.CountAsync(w => w.UserId == userId);

                return Json(new
                {
                    success = true,
                    message = "Đã thêm vào danh sách yêu thích.",
                    wishlistCount = wishlistCount
                });
            }
            catch (Exception ex)
            {
                // Log error nếu cần
                // _logger.LogError(ex, "Error adding product {ProductId} to wishlist for user {UserId}", productId, User.FindFirstValue(ClaimTypes.NameIdentifier));

                return Json(new { success = false, message = "Có lỗi xảy ra. Vui lòng thử lại." });
            }
        }

        // Xóa sản phẩm khỏi yêu thích
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromWishlist(int productId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var wishlistItem = await _context.Wishlists
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId);

                if (wishlistItem == null)
                {
                    return Json(new { success = false, message = "Sản phẩm không có trong danh sách yêu thích." });
                }

                _context.Wishlists.Remove(wishlistItem);
                await _context.SaveChangesAsync();

                // Đếm số lượng items trong wishlist
                var wishlistCount = await _context.Wishlists.CountAsync(w => w.UserId == userId);

                return Json(new
                {
                    success = true,
                    message = "Đã xóa khỏi danh sách yêu thích.",
                    wishlistCount = wishlistCount
                });
            }
            catch (Exception ex)
            {
                // Log error nếu cần
                // _logger.LogError(ex, "Error removing product {ProductId} from wishlist for user {UserId}", productId, User.FindFirstValue(ClaimTypes.NameIdentifier));

                return Json(new { success = false, message = "Có lỗi xảy ra. Vui lòng thử lại." });
            }
        }

        // Kiểm tra sản phẩm có trong wishlist không
        [HttpGet]
        public async Task<IActionResult> CheckWishlistStatus(int productId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var isInWishlist = await _context.Wishlists
                    .AnyAsync(w => w.UserId == userId && w.ProductId == productId);

                return Json(new { isInWishlist = isInWishlist });
            }
            catch (Exception ex)
            {
                return Json(new { isInWishlist = false });
            }
        }

        // Lấy số lượng items trong wishlist
        [HttpGet]
        public async Task<IActionResult> GetWishlistCount()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var count = await _context.Wishlists.CountAsync(w => w.UserId == userId);

                return Json(new { count = count });
            }
            catch (Exception ex)
            {
                return Json(new { count = 0 });
            }
        }

        // Toggle wishlist status (thêm hoặc xóa)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleWishlist(int productId)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var existingItem = await _context.Wishlists
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId);

                if (existingItem != null)
                {
                    // Xóa khỏi wishlist
                    _context.Wishlists.Remove(existingItem);
                    await _context.SaveChangesAsync();

                    var wishlistCount = await _context.Wishlists.CountAsync(w => w.UserId == userId);

                    return Json(new
                    {
                        success = true,
                        message = "Đã xóa khỏi danh sách yêu thích.",
                        isInWishlist = false,
                        wishlistCount = wishlistCount
                    });
                }
                else
                {
                    // Kiểm tra sản phẩm có tồn tại không
                    var product = await _context.Products.FindAsync(productId);
                    if (product == null)
                    {
                        return Json(new { success = false, message = "Sản phẩm không tồn tại." });
                    }

                    // Thêm vào wishlist
                    var wishlistItem = new Wishlist
                    {
                        UserId = userId,
                        ProductId = productId,
                        CreatedAt = DateTime.Now
                    };

                    _context.Wishlists.Add(wishlistItem);
                    await _context.SaveChangesAsync();

                    var wishlistCount = await _context.Wishlists.CountAsync(w => w.UserId == userId);

                    return Json(new
                    {
                        success = true,
                        message = "Đã thêm vào danh sách yêu thích.",
                        isInWishlist = true,
                        wishlistCount = wishlistCount
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra. Vui lòng thử lại." });
            }
        }

        // Xóa nhiều items cùng lúc
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveMultiple(int[] productIds)
        {
            try
            {
                if (productIds == null || productIds.Length == 0)
                {
                    return Json(new { success = false, message = "Không có sản phẩm nào được chọn." });
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                var itemsToRemove = await _context.Wishlists
                    .Where(w => w.UserId == userId && productIds.Contains(w.ProductId))
                    .ToListAsync();

                if (itemsToRemove.Any())
                {
                    _context.Wishlists.RemoveRange(itemsToRemove);
                    await _context.SaveChangesAsync();
                }

                var wishlistCount = await _context.Wishlists.CountAsync(w => w.UserId == userId);

                return Json(new
                {
                    success = true,
                    message = $"Đã xóa {itemsToRemove.Count} sản phẩm khỏi danh sách yêu thích.",
                    wishlistCount = wishlistCount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra. Vui lòng thử lại." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> AddToCartAjax(int productId, int quantity = 1)
        {
            try
            {
                if (!User.Identity.IsAuthenticated)
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập để thêm sản phẩm vào giỏ hàng." });
                }

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Kiểm tra sản phẩm có tồn tại không
                var product = await _context.Products.FindAsync(productId);
                if (product == null)
                {
                    return Json(new { success = false, message = "Sản phẩm không tồn tại." });
                }

                // Kiểm tra tồn kho
                if (product.Stock < quantity)
                {
                    return Json(new { success = false, message = "Sản phẩm không đủ số lượng trong kho." });
                }

                // Tìm hoặc tạo giỏ hàng
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart == null)
                {
                    cart = new Cart
                    {
                        UserId = userId,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    _context.Carts.Add(cart);
                    await _context.SaveChangesAsync();
                }

                // Kiểm tra sản phẩm đã có trong giỏ hàng chưa
                var existingItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);

                if (existingItem != null)
                {
                    // Cập nhật số lượng
                    existingItem.Quantity += quantity;
                    existingItem.Subtotal = existingItem.Price * existingItem.Quantity;
                    _context.CartItems.Update(existingItem);
                }
                else
                {
                    // Thêm item mới
                    var cartItem = new CartItem
                    {
                        CartId = cart.CartId,
                        ProductId = productId,
                        ProductName = product.Name,
                        Price = product.Price,
                        Quantity = quantity,
                        Subtotal = product.Price * quantity
                    };
                    _context.CartItems.Add(cartItem);
                }

                // Cập nhật thời gian
                cart.UpdatedAt = DateTime.Now;
                _context.Carts.Update(cart);

                await _context.SaveChangesAsync();

                // Đếm số lượng items trong giỏ hàng
                var cartCount = await _context.CartItems
                    .Where(ci => ci.Cart.UserId == userId)
                    .CountAsync();

                return Json(new
                {
                    success = true,
                    message = $"Đã thêm {quantity} sản phẩm vào giỏ hàng.",
                    cartCount = cartCount
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra. Vui lòng thử lại." });
            }
        }

    }
}
