using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DOAN_LAPTRINHWEB.Migrations; // Đảm bảo đúng namespace của DbContext
using DOAN_LAPTRINHWEB.Models; // Đảm bảo đúng namespace của Models

namespace DOAN_LAPTRINHWEB.Services
{
    public class WishlistService : IWishlistService
    {
        private readonly HomeStylesDbContext _context;

        public WishlistService(HomeStylesDbContext context)
        {
            _context = context;
        }

        public async Task<bool> AddToWishlistAsync(string userId, int productId)
        {
            // 1. Kiểm tra xem sản phẩm có tồn tại không
            var productExists = await _context.Products.AnyAsync(p => p.ProductId == productId);
            if (!productExists)
            {
                // Sản phẩm không tồn tại
                return false;
            }

            // 2. Kiểm tra xem sản phẩm đã có trong wishlist của người dùng này chưa
            var existingItem = await _context.WishlistItems
                                             .AnyAsync(wi => wi.UserId == userId && wi.ProductId == productId);
            if (existingItem)
            {
                // Sản phẩm đã có trong wishlist rồi
                return false;
            }

            // 3. Tạo mới WishlistItem
            var wishlistItem = new WishlistItem
            {
                UserId = userId,
                ProductId = productId,
                AddedDate = DateTime.UtcNow // Đặt thời gian thêm vào wishlist
            };

            // 4. Thêm vào DbContext và lưu thay đổi
            _context.WishlistItems.Add(wishlistItem);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveFromWishlistAsync(string userId, int productId)
        {
            // 1. Tìm mục WishlistItem cần xóa
            var wishlistItem = await _context.WishlistItems
                                             .FirstOrDefaultAsync(wi => wi.UserId == userId && wi.ProductId == productId);

            if (wishlistItem == null)
            {
                // Không tìm thấy sản phẩm trong wishlist của người dùng này
                return false;
            }

            // 2. Xóa khỏi DbContext và lưu thay đổi
            _context.WishlistItems.Remove(wishlistItem);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<Product>> GetUserWishlistAsync(string userId)
        {
            // Truy vấn các WishlistItem của người dùng và eager load thông tin Product
            var wishlistProducts = await _context.WishlistItems
                                                 .Where(wi => wi.UserId == userId)
                                                 .Include(wi => wi.Product) // Tải thông tin sản phẩm liên quan
                                                    .ThenInclude(p => p.ProductImages.Where(pi => pi.IsPrimary == true).Take(1)) // Tải ảnh chính nếu có
                                                 .Select(wi => wi.Product) // Chỉ lấy đối tượng Product
                                                 .ToListAsync();
            return wishlistProducts;
        }

        public async Task<bool> IsProductInWishlistAsync(string userId, int productId)
        {
            // Kiểm tra sự tồn tại của WishlistItem dựa trên UserId và ProductId
            return await _context.WishlistItems
                                 .AnyAsync(wi => wi.UserId == userId && wi.ProductId == productId);
        }
    }
}