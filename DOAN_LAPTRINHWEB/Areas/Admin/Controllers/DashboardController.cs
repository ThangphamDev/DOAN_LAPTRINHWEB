using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DOAN_LAPTRINHWEB.Models;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace DOAN_LAPTRINHWEB.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Administrator")]
    public class DashboardController : Controller
    {
        private readonly HomeStylesDbContext _context;

        public DashboardController(HomeStylesDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Get dashboard statistics
            ViewBag.TotalProducts = await _context.Products.CountAsync();
            ViewBag.TotalOrders = await _context.Orders.CountAsync();
            ViewBag.TotalUsers = await _context.Users.CountAsync();
            ViewBag.TotalCategories = await _context.Categories.CountAsync();

            // Get recent orders
            ViewBag.RecentOrders = await _context.Orders
                .Include(o => o.User)
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Get top selling products
            ViewBag.TopProducts = await _context.OrderItems
                .Include(oi => oi.Product)
                .GroupBy(oi => oi.ProductId)
                .Select(g => new {
                    Product = g.First().Product,
                    TotalSold = g.Sum(oi => oi.Quantity)
                })
                .OrderByDescending(x => x.TotalSold)
                .Take(5)
                .ToListAsync();

            return View();
        }
    }
}