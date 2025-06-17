using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DOAN_LAPTRINHWEB.Models;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace DOAN_LAPTRINHWEB.Controllers
{
    public class ProductsController : Controller
    {
        private readonly HomeStylesDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public ProductsController(HomeStylesDbContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }

        // GET: Products
        public async Task<IActionResult> Index(int? categoryId)
        {
            IQueryable<Product> productsQuery = _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Include(p => p.Reviews);

            // Lọc theo danh mục nếu có categoryId
            if (categoryId.HasValue && categoryId > 0)
            {
                productsQuery = productsQuery.Where(p => p.CategoryId == categoryId);

                // Lưu tên danh mục đã chọn để hiển thị
                var categoryName = await _context.Categories
                    .Where(c => c.CategoryId == categoryId)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync();

                ViewBag.SelectedCategoryId = categoryId;
                ViewBag.SelectedCategoryName = categoryName;
            }

            var products = await productsQuery.ToListAsync();
            return View(products);
        }
        // GET: Products/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .Include(p => p.Reviews)
                    .ThenInclude(r => r.User)
                .Include(p => p.Tags)
                .FirstOrDefaultAsync(m => m.ProductId == id);

            if (product == null)
            {
                return NotFound();
            }

            // Lấy sản phẩm liên quan từ cùng danh mục, loại trừ sản phẩm hiện tại
            var relatedProducts = await _context.Products
                .Where(p => p.CategoryId == product.CategoryId && p.ProductId != product.ProductId)
                .Include(p => p.ProductImages)
                .OrderBy(p => Guid.NewGuid()) // Sắp xếp ngẫu nhiên
                .Take(4) // Lấy tối đa 4 sản phẩm
                .ToListAsync();

            ViewBag.RelatedProducts = relatedProducts;

            return View(product);
        }

    }
}