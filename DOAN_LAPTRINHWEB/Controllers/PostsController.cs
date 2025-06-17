using DOAN_LAPTRINHWEB.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DOAN_LAPTRINHWEB.Controllers
{
    [Authorize]
    public class PostsController : Controller
    {
        private readonly HomeStylesDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PostsController(HomeStylesDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Posts/MyPosts - Danh sách bài viết của người dùng hiện tại có lọc
        public async Task<IActionResult> MyPosts(string statusFilter)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var postsQuery = _context.Posts
                .Include(p => p.PostType)
                .Where(p => p.UserId == userId && !p.IsDeleted);

            // Nếu có chọn trạng thái lọc thì thêm điều kiện
            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "All")
            {
                postsQuery = postsQuery.Where(p => p.ApprovalStatus == statusFilter);
            }

            var posts = await postsQuery
                .OrderByDescending(p => p.CreatedDate)
                .ToListAsync();

            ViewData["Title"] = "Bài viết của tôi";
            ViewBag.StatusFilter = statusFilter;

            return View(posts);
        }
        [HttpGet]
        public async Task<IActionResult> FilterByStatus(string statusFilter)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var query = _context.Posts
                .Include(p => p.PostType)
                .Where(p => p.UserId == userId && !p.IsDeleted);

            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "All")
            {
                query = query.Where(p => p.ApprovalStatus == statusFilter);
            }

            var filteredPosts = await query
                .OrderByDescending(p => p.CreatedDate)
                .ToListAsync();

            return PartialView("_PostListPartial", filteredPosts); // tạo file partial view này
        }



        // GET: Posts/AllPosts - Danh sách tất cả bài viết (Admin)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AllPosts()
        {
            var posts = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.PostType)
                .Include(p => p.ApprovedBy)
                .OrderByDescending(p => p.CreatedDate)
                .ToListAsync();
            ViewData["Title"] = "Tất cả bài đăng";
            return View(posts);
        }
        // GET: Posts/Create - Form tạo bài viết
        public IActionResult Create()
        {
            ViewData["PostTypes"] = _context.PostTypes.ToList();
            return View();
        }

        // POST: Posts/Create - Thêm bài viết mới
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePostDto dto)
        {
            if (ModelState.IsValid)
            {
                // Gán chuỗi rỗng nếu ImageUrls bị null (tránh lỗi khi insert vào DB)
                dto.Content = dto.Content.Replace("../uploads/", "/uploads/");
                dto.ImageUrls ??= "";
                var post = new Post
                {
                    Title = dto.Title,
                    Content = dto.Content,
                    ImageUrls = dto.ImageUrls,
                    PostTypeId = dto.PostTypeId,
                    UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    CreatedDate = DateTime.UtcNow,
                    ApprovalStatus = "Pending",
                    IsDeleted = false
                };

                _context.Add(post);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(MyPosts));
            }

            ViewData["PostTypes"] = _context.PostTypes.ToList();
            return View(dto);
        }
      


        // GET: Posts/Edit/5 - Form sửa bài viết
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var post = await _context.Posts.FindAsync(id);
            if (post == null || post.IsDeleted)
            {
                return NotFound();
            }

            if (post.UserId != User.FindFirstValue(ClaimTypes.NameIdentifier) && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            ViewData["PostTypes"] = _context.PostTypes.ToList();
            return View(post);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CreatePostDto dto)
        {
            if (ModelState.IsValid)
            {
                dto.ImageUrls ??= "";
                var existingPost = await _context.Posts.FindAsync(id);
                if (existingPost == null || existingPost.IsDeleted)
                {
                    return NotFound();
                }

                if (existingPost.UserId != User.FindFirstValue(ClaimTypes.NameIdentifier) && !User.IsInRole("Admin"))
                {
                    return Forbid();
                }

                existingPost.Title = dto.Title;
                existingPost.Content = dto.Content.Replace("../uploads/", "/uploads/");
                existingPost.ImageUrls = dto.ImageUrls ?? "";
                existingPost.PostTypeId = dto.PostTypeId;
                existingPost.ApprovalStatus = "Pending";

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(MyPosts));
            }

            ViewData["PostTypes"] = _context.PostTypes.ToList();
            return View(dto);
        }


        // GET: Posts/Delete/5 - Form xác nhận xóa bài viết
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var post = await _context.Posts
                .Include(p => p.PostType)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (post == null || post.IsDeleted)
            {
                return NotFound();
            }

            if (post.UserId != User.FindFirstValue(ClaimTypes.NameIdentifier) && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            return View(post);
        }

        // POST: Posts/Delete/5 - Xóa bài viết (xóa mềm)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var post = await _context.Posts.FindAsync(id);
            if (post == null || post.IsDeleted)
            {
                return NotFound();
            }

            if (post.UserId != User.FindFirstValue(ClaimTypes.NameIdentifier) && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            post.IsDeleted = true;
            _context.Update(post);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(MyPosts));
        }

        // GET: Posts/Approve/5 - Form duyệt bài viết (Admin)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var post = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.PostType)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (post == null || post.IsDeleted)
            {
                return NotFound();
            }

            return View(post);
        }

        // POST: Posts/Approve/5 - Lưu trạng thái duyệt (Admin)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(int id, string ApprovalStatus, string ApprovalComment)
        {
            var post = await _context.Posts.FindAsync(id);
            if (post == null || post.IsDeleted)
            {
                return NotFound();
            }

            post.ApprovalStatus = ApprovalStatus;
            post.ApprovalComment = ApprovalComment;
            post.ApprovalDate = DateTime.UtcNow;
            post.ApprovedById = User.FindFirstValue(ClaimTypes.NameIdentifier);

            _context.Update(post);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(AllPosts));
        }
        public async Task<IActionResult> Details(int id)
        {
            var post = await _context.Posts
                .Include(p => p.PostType)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null)
            {
                return NotFound();
            }

            return View(post);
        }
        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

            // ✅ Tạo thư mục nếu chưa tồn tại
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

        private bool PostExists(int id)
        {
            return _context.Posts.Any(e => e.Id == id && !e.IsDeleted);
        }

    }
}
