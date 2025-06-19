using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DOAN_LAPTRINHWEB.Models;

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
            var productExists = await _context.Products.AnyAsync(p => p.ProductId == productId); 
            if (!productExists)
            {
                return false;
            }
            var existingItem = await _context.WishlistItems.AnyAsync(wi => wi.UserId == userId && wi.ProductId == productId);
            if (existingItem)
            {
                return false;
            }
            var wishlistItem = new WishlistItem
            {
                UserId = userId,
                ProductId = productId,
                AddedDate = DateTime.UtcNow
            };
            _context.WishlistItems.Add(wishlistItem);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveFromWishlistAsync(string userId, int productId)
        {
            var wishlistItem = await _context.WishlistItems
                                             .FirstOrDefaultAsync(wi => wi.UserId == userId && wi.ProductId == productId);

            if (wishlistItem == null)
            {
                // Không tìm thấy sản phẩm trong wishlist của người dùng này
                return false;
            }
            _context.WishlistItems.Remove(wishlistItem);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<Product>> GetUserWishlistAsync(string userId)
        {
            var wishlistProducts = await _context.WishlistItems
                                                 .Where(wi => wi.UserId == userId)
                                                 .Include(wi => wi.Product) 
                                                     .ThenInclude(p => p.ProductImages.Where(pi => pi.IsPrimary == true).Take(1)) 
                                                 .Select(wi => wi.Product)
                                                 .ToListAsync();
            return wishlistProducts;
        }

        public async Task<bool> IsProductInWishlistAsync(string userId, int productId)
        {
            return await _context.WishlistItems
                                 .AnyAsync(wi => wi.UserId == userId && wi.ProductId == productId);
        }
    }
}