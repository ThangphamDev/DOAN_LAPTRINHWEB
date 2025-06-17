#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DOAN_LAPTRINHWEB.Models; 
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore; 

namespace DOAN_LAPTRINHWEB.Areas.Identity.Pages.Account.Manage
{
    public class OrdersModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly HomeStylesDbContext _context; 

        public OrdersModel(
            UserManager<ApplicationUser> userManager,
            HomeStylesDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }
        public IList<Order> UserOrders { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Không th? t?i ng??i dùng v?i ID '{_userManager.GetUserId(User)}'.");
            }
            UserOrders = await _context.Orders
                                    .Where(o => o.UserId == user.Id)
                                    .OrderByDescending(o => o.CreatedAt)
                                    .ToListAsync();
            return Page();
        }
    }
}