using System.Collections.Generic;
using System.Threading.Tasks;
using DOAN_LAPTRINHWEB.Models; 
namespace DOAN_LAPTRINHWEB.Services
{
    public interface IWishlistService
    {
        Task<bool> AddToWishlistAsync(string userId, int productId);
        Task<bool> RemoveFromWishlistAsync(string userId, int productId);
        Task<IEnumerable<Product>> GetUserWishlistAsync(string userId);
        Task<bool> IsProductInWishlistAsync(string userId, int productId);
    }
}