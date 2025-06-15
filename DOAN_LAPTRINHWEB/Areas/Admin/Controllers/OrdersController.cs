using DOAN_LAPTRINHWEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace DOAN_LAPTRINHWEB.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Administrator")]
    public class OrdersController : Controller
    {
        private readonly HomeStylesDbContext _context;

        public OrdersController(HomeStylesDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Orders
        public async Task<IActionResult> Index(string status = null)
        {
            IQueryable<Order> ordersQuery = _context.Orders
                .Include(o => o.User)
                .OrderByDescending(o => o.CreatedAt);

            // Filter by status if provided
            if (!string.IsNullOrEmpty(status))
            {
                ordersQuery = ordersQuery.Where(o => o.OrderStatus == status);
                ViewBag.CurrentStatus = status;
            }

            var orders = await ordersQuery.ToListAsync();

            // For status filter dropdown
            ViewBag.Statuses = new[] { "Pending", "Processing", "Shipped", "Delivered", "Cancelled" };

            return View(orders);
        }

        // GET: Admin/Orders/Details/5
        
    }
}