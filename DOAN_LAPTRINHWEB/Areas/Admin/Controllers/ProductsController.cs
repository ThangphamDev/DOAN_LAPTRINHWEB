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

namespace DOAN_LAPTRINHWEB.Areas.Admin.Controllers
{
    [Area("Admin")]
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

        // GET: Products/Create
        public IActionResult Create()
        {
            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name");
            return View();
        }

        // POST: Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Description,Price,Stock,CategoryId")] Product product,
            IFormFile PrimaryImage, List<IFormFile> AdditionalImages)
        {
            if (ModelState.IsValid)
            {
                // Set creation date
                product.CreatedAt = DateTime.Now;
                product.UpdatedAt = DateTime.Now;

                // Add product to context
                _context.Add(product);
                await _context.SaveChangesAsync();

                // Process images after product is saved (to get ProductId)
                await ProcessUploadedImages(product.ProductId, PrimaryImage, AdditionalImages);

                return RedirectToAction(nameof(Index));
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name", product.CategoryId);
            return View(product);
        }

        // GET: Products/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
            {
                return NotFound();
            }

            TempData.Remove("SuccessMessage");

            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name", product.CategoryId);
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product, IFormFile? PrimaryImage, List<IFormFile>? AdditionalImages)
        {
            // Check if the request has the correct product ID
            if (id != product.ProductId)
            {
                return NotFound();
            }

            // Log model state for debugging
            foreach (var state in ModelState)
            {
                Console.WriteLine($"Key: {state.Key}, Valid: {state.Value.ValidationState == Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Valid}");
                foreach (var error in state.Value.Errors)
                {
                    Console.WriteLine($"Error: {error.ErrorMessage}");
                }
            }

            // Check if we have a valid model
            if (ModelState.IsValid)
            {
                try
                {
                    // Get the existing product to preserve CreatedAt if not provided
                    var existingProduct = await _context.Products.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.ProductId == id);

                    if (existingProduct == null)
                    {
                        return NotFound();
                    }

                    // Set creation date from existing record if not provided
                    if (product.CreatedAt == null)
                    {
                        product.CreatedAt = existingProduct.CreatedAt;
                    }

                    // Always update the modify date
                    product.UpdatedAt = DateTime.Now;

                    // Apply the update to the product in database
                    _context.Update(product);
                    await _context.SaveChangesAsync();

                    // Process images - using the pattern from the previous implementation
                    await ProcessUploadedImages(product.ProductId, PrimaryImage, AdditionalImages);

                    TempData["SuccessMessage"] = "Sản phẩm đã được cập nhật thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    // Ghi log lỗi để debug
                    Console.WriteLine($"Lỗi khi cập nhật sản phẩm: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");

                    // Thêm lỗi vào ModelState để hiển thị cho người dùng
                    ModelState.AddModelError("", $"Lỗi khi cập nhật: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Model không hợp lệ");
            }

            // If we got this far, something failed
            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "Name", product.CategoryId);
            return View(product);
        }

        // GET: Products/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(m => m.ProductId == id);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        // POST: Products/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product != null)
            {
                // Delete product images from file system
                foreach (var image in product.ProductImages)
                {
                    DeleteImageFile(image.ImageUrl);
                }

                // Delete product from database
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Products/RemoveImage/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveImage(int id)
        {
            var image = await _context.ProductImages.FindAsync(id);
            if (image == null)
            {
                return Json(new { success = false, message = "Không tìm thấy ảnh" });
            }

            try
            {

                DeleteImageFile(image.ImageUrl);

                _context.ProductImages.Remove(image);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting image: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.ProductId == id);
        }

        private async Task ProcessUploadedImages(int productId, IFormFile primaryImage, List<IFormFile> additionalImages)
        {
            // Make sure both primaryImage and additionalImages are not null
            additionalImages = additionalImages ?? new List<IFormFile>();

            string uploadsFolder = Path.Combine(_hostEnvironment.WebRootPath, "images", "products", productId.ToString());

            // Create directory if it doesn't exist
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Process primary image
            if (primaryImage != null && primaryImage.Length > 0)
            {
                try
                {
                    // Check if there's an existing primary image
                    var existingPrimaryImage = await _context.ProductImages
                        .FirstOrDefaultAsync(i => i.ProductId == productId && i.IsPrimary == true);

                    if (existingPrimaryImage != null)
                    {
                        // Delete old primary image file
                        DeleteImageFile(existingPrimaryImage.ImageUrl);

                        // Update existing record
                        existingPrimaryImage.ImageUrl = await SaveImage(productId, primaryImage, uploadsFolder);
                        _context.Update(existingPrimaryImage);
                    }
                    else
                    {
                        // Create new primary image record
                        _context.ProductImages.Add(new ProductImage
                        {
                            ProductId = productId,
                            ImageUrl = await SaveImage(productId, primaryImage, uploadsFolder),
                            IsPrimary = true
                        });
                    }

                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // Log the exception but don't throw
                    Console.WriteLine($"Error processing primary image: {ex.Message}");
                }
            }

            // Process additional images
            if (additionalImages.Any())
            {
                try
                {
                    foreach (var imageFile in additionalImages)
                    {
                        if (imageFile != null && imageFile.Length > 0)
                        {
                            _context.ProductImages.Add(new ProductImage
                            {
                                ProductId = productId,
                                ImageUrl = await SaveImage(productId, imageFile, uploadsFolder),
                                IsPrimary = false
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    // Log the exception but don't throw
                    Console.WriteLine($"Error processing additional images: {ex.Message}");
                }
            }
        }

        private async Task<string> SaveImage(int productId, IFormFile imageFile, string folderPath)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                throw new ArgumentException("No valid image file provided");
            }

            // Create unique filename
            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
            string filePath = Path.Combine(folderPath, fileName);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            // Return relative URL path for the database
            return $"/images/products/{productId}/{fileName}";
        }

        private void DeleteImageFile(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
                return;

            try
            {
                string fileName = Path.GetFileName(imageUrl);
                string folderPath = imageUrl.Replace("/", "\\").Replace(fileName, "");
                string fullPath = Path.Combine(_hostEnvironment.WebRootPath, folderPath.TrimStart('\\'), fileName);

                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }
            catch (Exception ex)
            {
                // Log but don't throw
                Console.WriteLine($"Error deleting file: {ex.Message}");
            }
        }
    }
}