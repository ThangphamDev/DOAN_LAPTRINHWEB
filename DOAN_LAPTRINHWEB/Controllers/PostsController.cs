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
        // GET: Posts - Trang chủ bài viết (hiển thị bài viết đã được duyệt)
        [AllowAnonymous]
        public async Task<IActionResult> Index(int? postTypeId, int page = 1, int pageSize = 10)
        {
            var postsQuery = _context.Posts
                .Include(p => p.User)
                .Include(p => p.PostType)
                .Where(p => p.ApprovalStatus == "Approved" && !p.IsDeleted);

            // Lọc theo loại bài viết nếu có
            if (postTypeId.HasValue)
            {
                postsQuery = postsQuery.Where(p => p.PostTypeId == postTypeId.Value);
            }

            var totalPosts = await postsQuery.CountAsync();
            var posts = await postsQuery
                .OrderByDescending(p => p.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Lấy thông tin like và comment cho từng bài viết
            var postIds = posts.Select(p => p.Id).ToList();

            // Lấy số lượng like cho tất cả bài viết
            var likeCounts = await _context.PostLikes
                .Where(l => postIds.Contains(l.PostId))
                .GroupBy(l => l.PostId)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PostId, x => x.Count);

            // Lấy số lượng comment cho tất cả bài viết
            var commentCounts = await _context.Comments
                .Where(c => postIds.Contains(c.PostId))
                .GroupBy(c => c.PostId)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PostId, x => x.Count);

            // Kiểm tra user hiện tại đã like bài viết nào chưa
            Dictionary<int, bool> userLikes = new Dictionary<int, bool>();
            if (User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var likedPosts = await _context.PostLikes
                    .Where(l => l.UserId == userId && postIds.Contains(l.PostId))
                    .Select(l => l.PostId)
                    .ToListAsync();

                foreach (var postId in postIds)
                {
                    userLikes[postId] = likedPosts.Contains(postId);
                }

                var currentUser = await _userManager.GetUserAsync(User);
                ViewBag.CurrentUser = currentUser;
            }

            ViewBag.PostTypes = await _context.PostTypes.ToListAsync();
            ViewBag.CurrentPostTypeId = postTypeId;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalPosts / pageSize);
            ViewBag.PageSize = pageSize;
            ViewBag.LikeCounts = likeCounts;
            ViewBag.CommentCounts = commentCounts;
            ViewBag.UserLikes = userLikes;

            return View(posts);
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
        public async Task<IActionResult> Create(CreatePostDto dto, IFormFile CoverImageFile)
        {
            if (ModelState.IsValid)
            {
                // Xử lý nội dung để thay thế đường dẫn tương đối
                dto.Content = dto.Content?.Replace("../uploads/", "/uploads/");

                // Xử lý URL hình ảnh
                string imageUrls = dto.ImageUrls ?? "";

                // Nếu có file hình ảnh đại diện được tải lên
                if (CoverImageFile != null && CoverImageFile.Length > 0)
                {
                    // Kiểm tra kích thước file (5MB)
                    if (CoverImageFile.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("CoverImageFile", "Kích thước file quá lớn (tối đa 5MB).");
                        ViewData["PostTypes"] = _context.PostTypes.ToList();
                        return View(dto);
                    }

                    // Kiểm tra loại file
                    var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif" };
                    if (!allowedTypes.Contains(CoverImageFile.ContentType))
                    {
                        ModelState.AddModelError("CoverImageFile", "Chỉ chấp nhận file hình ảnh JPG, PNG hoặc GIF.");
                        ViewData["PostTypes"] = _context.PostTypes.ToList();
                        return View(dto);
                    }

                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

                    // Tạo thư mục nếu chưa tồn tại
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    // Tạo tên file duy nhất
                    var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(CoverImageFile.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    // Lưu file
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await CoverImageFile.CopyToAsync(stream);
                    }

                    // Cập nhật URL hình ảnh (ghi đè lên nếu đã có URL)
                    imageUrls = $"/uploads/{uniqueFileName}";
                }

                // Nếu không có URL hình ảnh riêng và không upload file, tìm URL từ nội dung
                if (string.IsNullOrEmpty(imageUrls) && !string.IsNullOrEmpty(dto.Content))
                {
                    // Biểu thức chính quy để tìm các thẻ hình ảnh trong nội dung HTML
                    var imgRegex = new System.Text.RegularExpressions.Regex(@"<img[^>]*src=""([^""]*)""[^>]*>");
                    var match = imgRegex.Match(dto.Content);
                    if (match.Success && match.Groups.Count > 1)
                    {
                        // Lấy URL hình ảnh đầu tiên từ nội dung
                        imageUrls = match.Groups[1].Value;
                    }
                }

                var post = new Post
                {
                    Title = dto.Title,
                    Content = dto.Content,
                    ImageUrls = imageUrls,
                    PostTypeId = dto.PostTypeId,
                    UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    CreatedDate = DateTime.UtcNow,
                    ApprovalStatus = "Approved",
                    IsDeleted = false
                };

                _context.Add(post);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Tạo bài viết thành công!";
                return RedirectToAction("Index");
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
        public async Task<IActionResult> Edit(int id, CreatePostDto dto, IFormFile CoverImageFile)
        {
            if (ModelState.IsValid)
            {
                var existingPost = await _context.Posts.FindAsync(id);
                if (existingPost == null || existingPost.IsDeleted)
                {
                    return NotFound();
                }

                if (existingPost.UserId != User.FindFirstValue(ClaimTypes.NameIdentifier) && !User.IsInRole("Admin"))
                {
                    return Forbid();
                }

                // Xử lý URL hình ảnh
                string imageUrls = dto.ImageUrls ?? "";

                // Nếu có file hình ảnh đại diện được tải lên
                if (CoverImageFile != null && CoverImageFile.Length > 0)
                {
                    // Kiểm tra kích thước file (5MB)
                    if (CoverImageFile.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("CoverImageFile", "Kích thước file quá lớn (tối đa 5MB).");
                        ViewData["PostTypes"] = _context.PostTypes.ToList();
                        return View(dto);
                    }

                    // Kiểm tra loại file
                    var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif" };
                    if (!allowedTypes.Contains(CoverImageFile.ContentType))
                    {
                        ModelState.AddModelError("CoverImageFile", "Chỉ chấp nhận file hình ảnh JPG, PNG hoặc GIF.");
                        ViewData["PostTypes"] = _context.PostTypes.ToList();
                        return View(dto);
                    }

                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

                    // Tạo thư mục nếu chưa tồn tại
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    // Tạo tên file duy nhất
                    var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(CoverImageFile.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    // Lưu file
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await CoverImageFile.CopyToAsync(stream);
                    }

                    // Cập nhật URL hình ảnh (ghi đè lên nếu đã có URL)
                    imageUrls = $"/uploads/{uniqueFileName}";
                }

                existingPost.Title = dto.Title;
                existingPost.Content = dto.Content?.Replace("../uploads/", "/uploads/");
                existingPost.ImageUrls = imageUrls;
                existingPost.PostTypeId = dto.PostTypeId;
                existingPost.ApprovalStatus = "Approved";

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
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null)
            {
                return NotFound();
            }

            // Lấy số lượng like
            var likesCount = await _context.PostLikes.CountAsync(l => l.PostId == id);
            ViewBag.LikesCount = likesCount;

            // Kiểm tra xem user hiện tại đã like bài viết chưa
            if (User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userLiked = await _context.PostLikes.AnyAsync(l => l.PostId == id && l.UserId == userId);
                ViewBag.UserLiked = userLiked;

                // Thêm CurrentUser vào ViewBag
                var currentUser = await _userManager.GetUserAsync(User);
                ViewBag.CurrentUser = currentUser;
            }
            else
            {
                ViewBag.UserLiked = false;
                ViewBag.CurrentUser = null;
            }

            // Lấy số lượng bình luận
            var commentsCount = await _context.Comments.CountAsync(c => c.PostId == id);
            ViewBag.CommentsCount = commentsCount;

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


        // ACTION: Like một bài viết
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> LikePost(int id)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Unauthorized(new { success = false, message = "Bạn cần đăng nhập để thích bài viết." });
            }

            var post = await _context.Posts.FindAsync(id);
            if (post == null || post.IsDeleted)
            {
                return NotFound(new { success = false, message = "Không tìm thấy bài viết." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var existingLike = await _context.PostLikes
                .FirstOrDefaultAsync(l => l.PostId == id && l.UserId == userId);

            if (existingLike != null)
            {
                // Nếu đã like rồi thì bỏ like
                _context.PostLikes.Remove(existingLike);
                await _context.SaveChangesAsync();

                // Đếm số lượng like mới
                var likeCount = await _context.PostLikes.CountAsync(l => l.PostId == id);

                return Json(new
                {
                    success = true,
                    liked = false,
                    likeCount = likeCount,
                    message = "Đã bỏ thích bài viết."
                });
            }
            else
            {
                // Nếu chưa like thì thêm like mới
                var newLike = new PostLike
                {
                    PostId = id,
                    UserId = userId,
                    CreatedDate = DateTime.UtcNow
                };

                await _context.PostLikes.AddAsync(newLike);
                await _context.SaveChangesAsync();

                // Đếm số lượng like mới
                var likeCount = await _context.PostLikes.CountAsync(l => l.PostId == id);

                return Json(new
                {
                    success = true,
                    liked = true,
                    likeCount = likeCount,
                    message = "Đã thích bài viết."
                });
            }
        }

        // ACTION: Thêm bình luận cho bài viết
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AddComment(int postId, string content, int? parentCommentId = null)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Unauthorized(new { success = false, message = "Bạn cần đăng nhập để bình luận." });
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return BadRequest(new { success = false, message = "Nội dung bình luận không được để trống." });
            }

            var post = await _context.Posts.FindAsync(postId);
            if (post == null || post.IsDeleted)
            {
                return NotFound(new { success = false, message = "Không tìm thấy bài viết." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            var comment = new Comment
            {
                PostId = postId,
                UserId = userId,
                Content = content,
                CreatedDate = DateTime.UtcNow,
                ParentCommentId = parentCommentId
            };

            await _context.Comments.AddAsync(comment);
            await _context.SaveChangesAsync();

            // Tạo dữ liệu để trả về cho việc render comment mới
            var commentData = new
            {
                id = comment.Id,
                content = comment.Content,
                createdDate = comment.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                userId = comment.UserId,
                userName = user.FullName ?? user.UserName,
                userAvatar = user.AvatarUrl ?? user.Avatar ?? "",
                parentCommentId = comment.ParentCommentId
            };

            return Json(new
            {
                success = true,
                comment = commentData,
                message = "Đã thêm bình luận."
            });
        }

        // ACTION: Lấy danh sách bình luận cho bài viết
        [HttpGet]
        public async Task<IActionResult> GetComments(int postId, int page = 1, int pageSize = 10)
        {
            var post = await _context.Posts.FindAsync(postId);
            if (post == null || post.IsDeleted)
            {
                return NotFound(new { success = false, message = "Không tìm thấy bài viết." });
            }

            var comments = await _context.Comments
                .Include(c => c.User)
                .Where(c => c.PostId == postId && c.ParentCommentId == null)
                .OrderByDescending(c => c.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Lấy các comments con (trả lời)
            var commentIds = comments.Select(c => c.Id).ToList();
            var childComments = await _context.Comments
                .Include(c => c.User)
                .Where(c => c.PostId == postId && c.ParentCommentId != null && commentIds.Contains(c.ParentCommentId.Value))
                .OrderBy(c => c.CreatedDate)
                .ToListAsync();

            var commentData = comments.Select(c => new
            {
                id = c.Id,
                content = c.Content,
                createdDate = c.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                userId = c.UserId,
                userName = c.User.FullName ?? c.User.UserName,
                userAvatar = c.User.AvatarUrl ?? c.User.Avatar ?? "",
                replies = childComments
                    .Where(cc => cc.ParentCommentId == c.Id)
                    .Select(cc => new
                    {
                        id = cc.Id,
                        content = cc.Content,
                        createdDate = cc.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                        userId = cc.UserId,
                        userName = cc.User.FullName ?? cc.User.UserName,
                        userAvatar = cc.User.AvatarUrl ?? cc.User.Avatar ?? "",
                        parentCommentId = cc.ParentCommentId
                    }).ToList()
            }).ToList();

            // Đếm tổng số bình luận
            var totalComments = await _context.Comments
                .Where(c => c.PostId == postId)
                .CountAsync();

            var totalPages = (int)Math.Ceiling(totalComments / (double)pageSize);

            return Json(new
            {
                success = true,
                comments = commentData,
                totalComments = totalComments,
                totalPages = totalPages,
                currentPage = page
            });
        }

        // ACTION: Xóa bình luận
        // ACTION: Xóa bình luận
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var comment = await _context.Comments.FindAsync(id);
            if (comment == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy bình luận." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            // Chỉ cho phép người tạo comment hoặc Admin xóa
            if (comment.UserId != userId && !User.IsInRole("Admin"))
            {
                // Sửa từ Forbid() thành Json() với status code 403
                return Json(new { success = false, message = "Bạn không có quyền xóa bình luận này." });
            }

            // Xóa cả các comment con
            var childComments = await _context.Comments
                .Where(c => c.ParentCommentId == id)
                .ToListAsync();

            _context.Comments.RemoveRange(childComments);
            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = "Đã xóa bình luận."
            });
        }

    }
}
