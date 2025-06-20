using System.Security.Claims;
using DOAN_LAPTRINHWEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DOAN_LAPTRINHWEB.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PostsController : Controller
    {
        private readonly HomeStylesDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PostsController(HomeStylesDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string statusFilter = "All", int page = 1, int pageSize = 10)
        {
            var query = _context.Posts
                .Include(p => p.User)
                .Include(p => p.PostType)
                .Include(p => p.ApprovedBy)
                .Where(p => !p.IsDeleted)
                .AsQueryable();

            // Lọc theo trạng thái
            switch (statusFilter)
            {
                case "Pending":
                    query = query.Where(p => p.ApprovalStatus == "Pending");
                    break;
                case "Approved":
                    query = query.Where(p => p.ApprovalStatus == "Approved");
                    break;
                case "Rejected":
                    query = query.Where(p => p.ApprovalStatus == "Rejected");
                    break;
                default:
                    // All - không lọc thêm
                    break;
            }

            var totalPosts = await query.CountAsync();
            var posts = await query
                .OrderByDescending(p => p.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.StatusFilter = statusFilter;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalPosts / pageSize);
            ViewBag.TotalPosts = totalPosts;

            // Thống kê
            ViewBag.PendingCount = await _context.Posts.Where(p => !p.IsDeleted && p.ApprovalStatus == "Pending").CountAsync();
            ViewBag.ApprovedCount = await _context.Posts.Where(p => !p.IsDeleted && p.ApprovalStatus == "Approved").CountAsync();
            ViewBag.RejectedCount = await _context.Posts.Where(p => !p.IsDeleted && p.ApprovalStatus == "Rejected").CountAsync();

            return View(posts);
        }

        // GET: Posts/Create - Form tạo bài viết
        public IActionResult Create()
        {
            ViewData["PostTypes"] = _context.PostTypes.ToList();
            return View();
        }

        // POST: Admin/Posts/Create
        // POST: Admin/Posts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePostDto dto, IFormFile CoverImageFile, List<IFormFile> ImageFiles)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var currentUser = await _userManager.GetUserAsync(User);
                    if (currentUser == null)
                    {
                        ModelState.AddModelError("", "Không thể xác định người dùng hiện tại.");
                        ViewData["PostTypes"] = await _context.PostTypes.ToListAsync();
                        return View(dto);
                    }

                    string imageUrls = "";
                    string coverImageUrl = "";

                    // Xử lý hình ảnh đại diện (KHÔNG BẮT BUỘC)
                    if (CoverImageFile != null && CoverImageFile.Length > 0)
                    {
                        // Kiểm tra kích thước file (5MB)
                        if (CoverImageFile.Length > 5 * 1024 * 1024)
                        {
                            ModelState.AddModelError("CoverImageFile", "Kích thước file quá lớn (tối đa 5MB).");
                            ViewData["PostTypes"] = await _context.PostTypes.ToListAsync();
                            return View(dto);
                        }

                        // Kiểm tra loại file
                        var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/jpg" };
                        if (!allowedTypes.Contains(CoverImageFile.ContentType.ToLower()))
                        {
                            ModelState.AddModelError("CoverImageFile", "Chỉ chấp nhận file hình ảnh JPG, PNG hoặc GIF.");
                            ViewData["PostTypes"] = await _context.PostTypes.ToListAsync();
                            return View(dto);
                        }

                        try
                        {
                            // Lưu file
                            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                            if (!Directory.Exists(uploadsFolder))
                            {
                                Directory.CreateDirectory(uploadsFolder);
                            }

                            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(CoverImageFile.FileName)}";
                            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await CoverImageFile.CopyToAsync(stream);
                            }

                            coverImageUrl = $"/uploads/{uniqueFileName}";
                        }
                        catch (Exception ex)
                        {
                            ModelState.AddModelError("CoverImageFile", $"Lỗi khi upload hình ảnh đại diện: {ex.Message}");
                            ViewData["PostTypes"] = await _context.PostTypes.ToListAsync();
                            return View(dto);
                        }
                    }

                    // Xử lý các hình ảnh bổ sung (KHÔNG BẮT BUỘC)
                    if (ImageFiles != null && ImageFiles.Any(f => f != null && f.Length > 0))
                    {
                        var uploadedImageUrls = new List<string>();
                        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        foreach (var file in ImageFiles.Where(f => f != null && f.Length > 0))
                        {
                            // Kiểm tra kích thước và loại file
                            if (file.Length > 5 * 1024 * 1024)
                            {
                                ModelState.AddModelError("ImageFiles", "Một hoặc nhiều file có kích thước quá lớn (tối đa 5MB).");
                                ViewData["PostTypes"] = await _context.PostTypes.ToListAsync();
                                return View(dto);
                            }

                            var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/jpg" };
                            if (!allowedTypes.Contains(file.ContentType.ToLower()))
                            {
                                ModelState.AddModelError("ImageFiles", "Chỉ chấp nhận file hình ảnh JPG, PNG hoặc GIF.");
                                ViewData["PostTypes"] = await _context.PostTypes.ToListAsync();
                                return View(dto);
                            }

                            try
                            {
                                var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
                                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                                using (var stream = new FileStream(filePath, FileMode.Create))
                                {
                                    await file.CopyToAsync(stream);
                                }

                                uploadedImageUrls.Add($"/uploads/{uniqueFileName}");
                            }
                            catch (Exception ex)
                            {
                                ModelState.AddModelError("ImageFiles", $"Lỗi khi upload hình ảnh: {ex.Message}");
                                ViewData["PostTypes"] = await _context.PostTypes.ToListAsync();
                                return View(dto);
                            }
                        }

                        if (uploadedImageUrls.Count > 0)
                        {
                            imageUrls = string.Join(",", uploadedImageUrls);
                        }
                    }

                    // Tạo bài viết mới (có thể không có hình ảnh)
                    var post = new Post
                    {
                        Title = dto.Title,
                        Content = dto.Content,
                        ImageUrls = string.IsNullOrEmpty(imageUrls) ? null : imageUrls, // Cho phép null
                        PostTypeId = dto.PostTypeId,
                        UserId = currentUser.Id,
                        CreatedDate = DateTime.UtcNow,
                        ApprovalStatus = "Approved", // Admin tự động được duyệt
                        ApprovedById = currentUser.Id,
                        ApprovalDate = DateTime.UtcNow,
                        IsDeleted = false
                    };

                    _context.Posts.Add(post);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Tạo bài viết thành công!";
                    return RedirectToAction(nameof(Index)); // Quay về trang Index
                }
                else
                {
                    // Log validation errors for debugging
                    var errors = ModelState
                        .Where(x => x.Value.Errors.Count > 0)
                        .Select(x => new { Field = x.Key, Errors = x.Value.Errors.Select(e => e.ErrorMessage) })
                        .ToList();

                    ModelState.AddModelError("", "Vui lòng kiểm tra lại thông tin đã nhập.");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Có lỗi xảy ra khi tạo bài viết: {ex.Message}");

                if (ex.InnerException != null)
                {
                    ModelState.AddModelError("", $"Chi tiết lỗi: {ex.InnerException.Message}");
                }
            }

            ViewData["PostTypes"] = await _context.PostTypes.ToListAsync();
            return View(dto);
        }

        // POST: Admin/Posts/UploadImage - Cho TinyMCE
        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

            // Tạo thư mục nếu chưa tồn tại
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Trả về đường dẫn để TinyMCE hiển thị ảnh
            var imageUrl = Url.Content($"~/uploads/{uniqueFileName}");
            return Json(new { location = imageUrl });
        }

        public async Task<IActionResult> Details(int id)
        {
            var post = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.PostType)
                .Include(p => p.ApprovedBy)
                .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (post == null)
            {
                return NotFound();
            }

            return View(post);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string approvalStatus, string approvalComment = "")
        {
            var post = await _context.Posts.FindAsync(id);
            if (post == null || post.IsDeleted)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);

            post.ApprovalStatus = approvalStatus;
            post.ApprovalComment = approvalComment;
            post.ApprovedById = currentUser.Id;
            post.ApprovalDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật trạng thái bài viết thành '{approvalStatus}'";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var post = await _context.Posts.FindAsync(id);
            if (post == null)
            {
                return NotFound();
            }

            post.IsDeleted = true;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xóa bài viết thành công";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> BulkAction(string action, int[] selectedPosts)
        {
            if (selectedPosts == null || selectedPosts.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn ít nhất một bài viết";
                return RedirectToAction(nameof(Index));
            }

            var posts = await _context.Posts
                .Where(p => selectedPosts.Contains(p.Id) && !p.IsDeleted)
                .ToListAsync();

            var currentUser = await _userManager.GetUserAsync(User);

            switch (action)
            {
                case "approve":
                    foreach (var post in posts)
                    {
                        post.ApprovalStatus = "Approved";
                        post.ApprovedById = currentUser.Id;
                        post.ApprovalDate = DateTime.UtcNow;
                    }
                    TempData["SuccessMessage"] = $"Đã duyệt {posts.Count} bài viết";
                    break;

                case "reject":
                    foreach (var post in posts)
                    {
                        post.ApprovalStatus = "Rejected";
                        post.ApprovedById = currentUser.Id;
                        post.ApprovalDate = DateTime.UtcNow;
                    }
                    TempData["SuccessMessage"] = $"Đã từ chối {posts.Count} bài viết";
                    break;

                case "delete":
                    foreach (var post in posts)
                    {
                        post.IsDeleted = true;
                    }
                    TempData["SuccessMessage"] = $"Đã xóa {posts.Count} bài viết";
                    break;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
