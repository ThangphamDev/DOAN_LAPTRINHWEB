using DOAN_LAPTRINHWEB.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

namespace DOAN_LAPTRINHWEB.Areas.Identity.Pages.Account.Manage
{
    public class OrderDetailsModel : PageModel
    {
        private readonly HomeStylesDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrderDetailsModel(HomeStylesDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public Order Order { get; set; }
        public Address ShippingAddress { get; set; }
        public Payment Payment { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Không thể tải thông tin người dùng với ID '{_userManager.GetUserId(User)}'.");
            }

            Order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.ProductImages)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.ProductVariant)
                .Include(o => o.User)
                .Include(o => o.ShippingAddress)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == user.Id);

            if (Order == null)
            {
                return NotFound("Không tìm thấy đơn hàng.");
            }

            // Lấy địa chỉ giao hàng từ Order
            ShippingAddress = Order.ShippingAddress;

            // Lấy thông tin thanh toán
            Payment = Order.Payments.FirstOrDefault();

            return Page();
        }
    }
}