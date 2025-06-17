using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DOAN_LAPTRINHWEB.Models;

namespace DOAN_LAPTRINHWEB.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly HomeStylesDbContext _context;

        public AdminController(HomeStylesDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            try
            {
                // Tổng số sản phẩm
                ViewBag.TotalProducts = await _context.Products.CountAsync();

                // Tổng số đơn hàng
                ViewBag.TotalOrders = await _context.Orders.CountAsync();

                // Tổng số người dùng (không tính Admin)
                ViewBag.TotalUsers = await _context.Users
                    .Where(u => !_context.UserRoles
                        .Join(_context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
                        .Where(x => x.Name == "Admin")
                        .Select(x => x.UserId)
                        .Contains(u.Id))
                    .CountAsync();

                // Doanh thu tháng này (chỉ tính đơn hàng đã hoàn thành)
                var currentMonth = DateTime.Now.Month;
                var currentYear = DateTime.Now.Year;

                ViewBag.MonthlyRevenue = await _context.Orders
                    .Where(o => o.CreatedAt.Month == currentMonth &&
                                o.CreatedAt.Year == currentYear &&
                                (o.OrderStatus.ToLower() == "delivered"))
                    .SumAsync(o => (decimal?)o.TotalAmount) ?? 0;

                // Đơn hàng gần đây (10 đơn gần nhất)
                ViewBag.RecentOrders = await _context.Orders
                    .Include(o => o.User)
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(10)
                    .Select(o => new
                    {
                        o.OrderId,
                        o.CreatedAt,
                        o.OrderStatus,
                        o.TotalAmount,
                        User = new
                        {
                            FullName = o.User != null ? o.User.FullName : "Khách vãng lai"
                        }
                    })
                    .ToListAsync();

                // Sản phẩm bán chạy (top 5)
                ViewBag.TopProducts = await _context.OrderItems
                    .Include(oi => oi.Product)
                    .GroupBy(oi => oi.ProductId)
                    .Select(g => new
                    {
                        Product = new
                        {
                            Name = g.First().Product.Name,
                            Price = g.First().Product.Price
                        },
                        TotalSold = g.Sum(oi => oi.Quantity)
                    })
                    .OrderByDescending(x => x.TotalSold)
                    .Take(5)
                    .ToListAsync();

                // Thống kê trạng thái đơn hàng
                var totalOrdersCount = await _context.Orders.CountAsync();
                if (totalOrdersCount > 0)
                {
                    var statusStats = await _context.Orders
                        .GroupBy(o => o.OrderStatus)
                        .Select(g => new
                        {
                            Status = g.Key,
                            Count = g.Count()
                        })
                        .ToListAsync();

                    ViewBag.OrderStatusStats = statusStats.Select(s => new
                    {
                        Status = GetStatusDisplayName(s.Status),
                        s.Count,
                        Percentage = Math.Round((double)s.Count / totalOrdersCount * 100, 1)
                    }).ToList();
                }
                else
                {
                    ViewBag.OrderStatusStats = new List<dynamic>();
                }

                // *** SỬA ĐỔI CHÍNH Ở ĐÂY ***
                // Thống kê theo tháng (6 tháng gần nhất)
                var sixMonthsAgo = DateTime.Now.AddMonths(-6).Date;
                var monthlyStatsData = await _context.Orders
                    .Where(o => o.CreatedAt >= sixMonthsAgo)
                    .GroupBy(o => new { o.CreatedAt.Year, o.CreatedAt.Month })
                    .Select(g => new
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        OrderCount = g.Count(),
                        // SỬA ĐỔI: Đảm bảo so sánh không phân biệt hoa thường để tính doanh thu chính xác
                        Revenue = g.Where(o => o.OrderStatus.ToLower() == "delivered")
                                     .Sum(o => (decimal?)o.TotalAmount) ?? 0
                    })
                    .OrderBy(x => x.Year)
                    .ThenBy(x => x.Month)
                    .ToListAsync();

                // Chuyển đổi dữ liệu để dễ sử dụng trong View
                ViewBag.MonthlyStats = monthlyStatsData.Select(s => new
                {
                    s.Year,
                    s.Month,
                    s.OrderCount,
                    s.Revenue
                }).ToList();

            }
            catch (Exception ex)
            {
                // Set default values nếu có lỗi
                ViewBag.TotalProducts = 0;
                ViewBag.TotalOrders = 0;
                ViewBag.TotalUsers = 0;
                ViewBag.MonthlyRevenue = 0m; // Sử dụng 0m cho decimal
                ViewBag.RecentOrders = new List<dynamic>();
                ViewBag.TopProducts = new List<dynamic>();
                ViewBag.OrderStatusStats = new List<dynamic>();
                ViewBag.MonthlyStats = new List<dynamic>();

                // Log error nếu cần
                ViewBag.ErrorMessage = "Có lỗi xảy ra khi tải dữ liệu dashboard: " + ex.Message;
            }

            return View();
        }

        private string GetStatusDisplayName(string status)
        {
            return status?.ToLower() switch
            {
                "pending" => "Chờ xử lý",
                "processing" => "Đang xử lý",
                "shipped" => "Đang giao hàng",
                "delivered" => "Đã nhận hàng", // Trạng thái này được tính là doanh thu
                "cancelled" => "Đã hủy",
                _ => status ?? "Không xác định"
            };
        }
    }
}
