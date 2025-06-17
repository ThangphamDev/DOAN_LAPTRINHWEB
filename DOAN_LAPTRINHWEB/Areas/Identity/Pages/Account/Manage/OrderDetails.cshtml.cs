using DOAN_LAPTRINHWEB.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

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

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            Order = await _context.Orders
         .Include(o => o.OrderItems)
             .ThenInclude(oi => oi.Product)
         .FirstOrDefaultAsync(o => o.OrderId == id);
            if (Order == null)
            {
                return NotFound();
            }

            return Page();
        }
    }
}
