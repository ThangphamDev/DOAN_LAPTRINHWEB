using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DOAN_LAPTRINHWEB.Models;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DOAN_LAPTRINHWEB.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewsController : ControllerBase
    {
        private readonly HomeStylesDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReviewsController(HomeStylesDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // POST: api/reviews
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> PostReview([FromBody] ReviewViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });
            }

            try
            {
                var user = await _userManager.GetUserAsync(User);

                if (user == null)
                {
                    return Unauthorized(new { success = false, message = "Vui lòng đăng nhập để đánh giá" });
                }

                // Kiểm tra xem sản phẩm có tồn tại không
                var product = await _context.Products.FindAsync(model.ProductId);
                if (product == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy sản phẩm" });
                }

                // Kiểm tra xem người dùng đã đánh giá sản phẩm này chưa
                var existingReview = await _context.Reviews
                    .FirstOrDefaultAsync(r => r.ProductId == model.ProductId && r.UserId == user.Id);

                if (existingReview != null)
                {
                    // Cập nhật đánh giá hiện có
                    existingReview.Rating = model.Rating;
                    existingReview.Comment = model.Comment;
                    existingReview.CreatedAt = DateTime.Now;
                    _context.Update(existingReview);
                }
                else
                {
                    // Tạo đánh giá mới
                    var review = new Review
                    {
                        ProductId = model.ProductId,
                        UserId = user.Id,
                        Rating = model.Rating,
                        Comment = model.Comment,
                        CreatedAt = DateTime.Now
                    };
                    _context.Reviews.Add(review);
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Đánh giá đã được ghi nhận" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }

    public class ReviewViewModel
    {
        public int ProductId { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
    }
}