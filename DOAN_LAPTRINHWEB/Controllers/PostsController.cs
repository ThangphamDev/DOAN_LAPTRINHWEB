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

        private string GetRelativeTime(DateTime utcTime)
        {
            var vietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var vietnamTime = TimeZoneInfo.ConvertTimeFromUtc(utcTime, vietnamTimeZone);
            var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone);

            var diff = now - vietnamTime;

            if (diff.TotalMinutes < 1)
                return "Vừa xong";
            else if (diff.TotalMinutes < 60)
                return $"{(int)diff.TotalMinutes} phút trước";
            else if (diff.TotalHours < 24)
                return $"{(int)diff.TotalHours} giờ trước";
            else if (diff.TotalDays < 7)
                return $"{(int)diff.TotalDays} ngày trước";
            else
                return vietnamTime.ToString("dd/MM/yyyy HH:mm");
        }

        public class FeaturedAuthorDto
        {
            public string UserId { get; set; }
            public string UserName { get; set; }
            public string UserAvatar { get; set; }
            public int TotalLikes { get; set; }
            public int PostCount { get; set; }
        }
        // GET: Posts - Trang chủ bài viết (hiển thị bài viết đã được duyệt)
        [AllowAnonymous]
        public async Task<IActionResult> Index(int? postTypeId, int page = 1)
        {
            const int pageSize = 10;

            var query = _context.Posts
                .Include(p => p.User)
                .Include(p => p.PostType)
                .Where(p => !p.IsDeleted && p.ApprovalStatus == "Approved")
                .AsQueryable();

            if (postTypeId.HasValue)
            {
                query = query.Where(p => p.PostTypeId == postTypeId.Value);
            }

            var posts = await query
                .OrderByDescending(p => p.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var totalPosts = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalPosts / pageSize);

            var postIds = posts.Select(p => p.Id).ToList();

            // Existing code for likes and comments...
            var likeCounts = await _context.PostLikes
                .Where(pl => postIds.Contains(pl.PostId))
                .GroupBy(pl => pl.PostId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            var commentCounts = await _context.Comments
                .Where(c => postIds.Contains(c.PostId))
                .GroupBy(c => c.PostId)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            Dictionary<int, bool> userLikes = new Dictionary<int, bool>();
            if (User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                userLikes = await _context.PostLikes
                    .Where(pl => postIds.Contains(pl.PostId) && pl.UserId == userId)
                    .ToDictionaryAsync(pl => pl.PostId, pl => true);
            }

            // **Lấy dữ liệu cho sidebar - CHỈ GIỮ ĐOẠN NÀY**
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

            // Lấy bài viết nổi bật
            try
            {
                var featuredPosts = await _context.Posts
                    .Include(p => p.User)
                    .Include(p => p.PostType)
                    .Where(p => p.CreatedDate >= thirtyDaysAgo && !p.IsDeleted && p.ApprovalStatus == "Approved")
                    .Select(p => new
                    {
                        Post = p,
                        LikeCount = _context.PostLikes.Count(pl => pl.PostId == p.Id)
                    })
                    .OrderByDescending(x => x.LikeCount)
                    .Take(3)
                    .Select(x => x.Post)
                    .ToListAsync();

                ViewBag.FeaturedPosts = featuredPosts;
            }
            catch (Exception ex)
            {
                ViewBag.FeaturedPosts = new List<Post>();
            }

            // Lấy tác giả nổi bật
            try
            {
                var featuredAuthors = await _context.Posts
                    .Include(p => p.User)
                    .Where(p => p.CreatedDate >= thirtyDaysAgo && p.User != null && !p.IsDeleted && p.ApprovalStatus == "Approved")
                    .GroupBy(p => p.UserId)
                    .Select(g => new FeaturedAuthorDto
                    {
                        UserId = g.Key,
                        UserName = g.First().User.FullName ?? "Ẩn danh",
                        UserAvatar = g.First().User.AvatarUrl ?? g.First().User.Avatar,
                        TotalLikes = _context.PostLikes.Count(pl => g.Any(p => p.Id == pl.PostId)),
                        PostCount = g.Count()
                    })
                    .OrderByDescending(x => x.TotalLikes)
                    .Take(3)
                    .ToListAsync();

                ViewBag.FeaturedAuthors = featuredAuthors;
            }
            catch (Exception ex)
            {
                ViewBag.FeaturedAuthors = new List<FeaturedAuthorDto>();
            }

            // ViewBag assignments...
            ViewBag.PostTypes = await _context.PostTypes.ToListAsync();
            ViewBag.CurrentPostTypeId = postTypeId;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.LikeCounts = likeCounts;
            ViewBag.CommentCounts = commentCounts;
            ViewBag.UserLikes = userLikes;
            ViewBag.CurrentUser = User.Identity.IsAuthenticated ?
                await _userManager.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)) : null;

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

            // Trả về JSON data thay vì HTML
            var postsData = filteredPosts.Select(post => new
            {
                id = post.Id,
                title = post.Title,
                content = System.Text.RegularExpressions.Regex.Replace(post.Content, "<.*?>", ""),
                createdDate = post.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                approvalStatus = post.ApprovalStatus,
                approvalComment = post.ApprovalComment,
                imageUrls = post.ImageUrls,
                postTypeName = post.PostType.Name
            }).ToList();

            return Json(new { success = true, posts = postsData });
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
                    ApprovalStatus = "Pending",
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

        public async Task<IActionResult> Details(int id)
        {
            var post = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.PostType)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (post == null)
            {
                return NotFound();
            }

            // Đếm số lượng like
            var likesCount = await _context.PostLikes.CountAsync(pl => pl.PostId == id);

            // Đếm TẤT CẢ bình luận (bao gồm cả replies)
            var commentsCount = await _context.Comments.CountAsync(c => c.PostId == id);

            // Kiểm tra user đã like chưa
            bool userLiked = false;
            if (User.Identity.IsAuthenticated)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                userLiked = await _context.PostLikes
                    .AnyAsync(pl => pl.PostId == id && pl.UserId == userId);
            }

            ViewBag.LikesCount = likesCount;
            ViewBag.CommentsCount = commentsCount;
            ViewBag.UserLiked = userLiked;
            ViewBag.CurrentUser = User.Identity.IsAuthenticated ?
                await _userManager.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)) : null;

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
                return Json(new { success = false, message = "Bạn cần đăng nhập để bình luận." });

            if (string.IsNullOrWhiteSpace(content))
                return Json(new { success = false, message = "Nội dung bình luận không được để trống." });

            try
            {
                var comment = new Comment
                {
                    PostId = postId,
                    UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    Content = content.Trim(),
                    CreatedDate = DateTime.UtcNow,
                    ParentCommentId = parentCommentId
                };

                _context.Comments.Add(comment);
                await _context.SaveChangesAsync();

                var savedComment = await _context.Comments
                    .Include(c => c.User)
                    .FirstAsync(c => c.Id == comment.Id);

                // Đếm lại tổng số comments sau khi thêm
                var totalComments = await _context.Comments.CountAsync(c => c.PostId == postId);

                return Json(new
                {
                    success = true,
                    comment = new
                    {
                        id = savedComment.Id,
                        content = savedComment.Content,
                        createdDate = GetRelativeTime(savedComment.CreatedDate),
                        userId = savedComment.UserId,
                        userName = savedComment.User?.FullName ?? "Ẩn danh",
                        userAvatar = savedComment.User?.AvatarUrl ?? savedComment.User?.Avatar
                    },
                    totalComments = totalComments // Trả về tổng số comments mới
                });
            }
            catch
            {
                return Json(new { success = false, message = "Đã xảy ra lỗi khi thêm bình luận." });
            }
        }

        // ACTION: Lấy danh sách bình luận cho bài viết
        [HttpGet]
        public async Task<IActionResult> GetComments(int postId, int page = 1, int pageSize = 10)
        {
            try
            {
                // Lấy comments gốc (không có parent)
                var comments = await _context.Comments
                    .Where(c => c.PostId == postId && c.ParentCommentId == null)
                    .Include(c => c.User)
                    .OrderByDescending(c => c.CreatedDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Đếm TẤT CẢ comments (bao gồm replies)
                var totalComments = await _context.Comments.CountAsync(c => c.PostId == postId);

                // Load replies cho từng comment
                var commentIds = comments.Select(c => c.Id).ToList();
                var replies = await _context.Comments
                    .Where(c => commentIds.Contains(c.ParentCommentId.Value))
                    .Include(c => c.User)
                    .OrderBy(c => c.CreatedDate)
                    .ToListAsync();

                var commentDtos = comments.Select(c => new
                {
                    id = c.Id,
                    content = c.Content,
                    createdDate = GetRelativeTime(c.CreatedDate),
                    userId = c.UserId,
                    userName = c.User?.FullName ?? "Ẩn danh",
                    userAvatar = c.User?.AvatarUrl ?? c.User?.Avatar,
                    replies = replies.Where(r => r.ParentCommentId == c.Id).Select(r => new
                    {
                        id = r.Id,
                        content = r.Content,
                        createdDate = GetRelativeTime(r.CreatedDate),
                        userId = r.UserId,
                        userName = r.User?.FullName ?? "Ẩn danh",
                        userAvatar = r.User?.AvatarUrl ?? r.User?.Avatar
                    }).ToList()
                }).ToList();

                return Json(new
                {
                    success = true,
                    comments = commentDtos,
                    totalComments = totalComments, // Tổng số TẤT CẢ comments
                    totalPages = (int)Math.Ceiling((double)comments.Count() / pageSize), // Phân trang theo comments gốc
                    currentPage = page
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Không thể tải bình luận." });
            }
        }

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
        public async Task<IActionResult> AuthorPosts(string userId, int page = 1)
        {
            const int pageSize = 10;

            var author = await _userManager.FindByIdAsync(userId);
            if (author == null)
            {
                return NotFound();
            }

            var posts = await _context.Posts
                .Include(p => p.User)
                .Include(p => p.PostType)
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var totalPosts = await _context.Posts.CountAsync(p => p.UserId == userId);
            var totalPages = (int)Math.Ceiling((double)totalPosts / pageSize);

            ViewBag.Author = author;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(posts);
        }

    }
}
