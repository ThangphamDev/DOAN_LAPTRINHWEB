using DOAN_LAPTRINHWEB.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace DOAN_LAPTRINHWEB.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly HomeStylesDbContext _context; // Giữ lại _context nếu bạn có các thao tác khác ngoài UserManager

        public UsersController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            HomeStylesDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        // GET: Admin/Users
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var userViewModels = new List<UserViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("Admin"))
                {
                    continue; // Bỏ qua người dùng có vai trò Admin
                }
                userViewModels.Add(new UserViewModel
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Status = user.Status,
                    EmailConfirmed = user.EmailConfirmed,
                    Roles = roles.ToList()
                });
            }

            // Đặt thông báo từ TempData nếu có
            if (TempData["Message"] != null)
            {
                ViewBag.Message = TempData["Message"];
                ViewBag.MessageType = TempData["MessageType"];
            }

            return View(userViewModels);
        }

        // GET: Admin/Users/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var orders = await _context.Orders
                .Where(o => o.UserId == id)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            var viewModel = new UserDetailsViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Status = user.Status,
                EmailConfirmed = user.EmailConfirmed,
                Roles = roles.ToList(),
                Orders = orders
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Lấy tất cả vai trò của người dùng hiện tại
            var userRoles = await _userManager.GetRolesAsync(user);

            // Lấy tất cả các vai trò KHÔNG PHẢI "Admin" từ RoleManager
            var allNonAdminRoles = _roleManager.Roles
                                                .Where(r => r.Name != "Admin")
                                                .ToList();

            var model = new UserEditViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Status = user.Status ?? "Đang hoạt động", // Mặc định nếu null
                EmailConfirmed = user.EmailConfirmed,
                Roles = allNonAdminRoles.Select(role => new RoleViewModel
                {
                    Id = role.Id,
                    Name = role.Name,
                    IsSelected = userRoles.Contains(role.Name) // Kiểm tra xem người dùng có vai trò này không
                }).ToList()
            };

            ViewBag.Statuses = new List<string> { "Đang hoạt động", "Tạm khóa" };
            return View(model);
        }


        // POST: Admin/Users/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserEditViewModel model)
        {
            // Kiểm tra ModelState trước khi tiếp tục
            // Loại bỏ lỗi cho FullName, Email, PhoneNumber nếu bạn không muốn validate chúng ở đây
            // Hoặc đảm bảo chúng được gửi đầy đủ từ client
            ModelState.Remove("FullName");
            ModelState.Remove("Email");
            ModelState.Remove("PhoneNumber");
            // Bất kỳ trường nào khác mà bạn không muốn validation tự động nếu chúng là hidden/readonly

            if (!ModelState.IsValid)
            {
                // Reload ViewBag data if validation fails
                ViewBag.Statuses = new List<string> { "Đang hoạt động", "Tạm khóa" };
                // Phải lấy lại roles để populate lại checkbox trên form nếu validation fail
                var userr = await _userManager.FindByIdAsync(model.Id);
                var userRoles = await _userManager.GetRolesAsync(userr);
                var allNonAdminRoles = _roleManager.Roles.Where(r => r.Name != "Admin").ToList();
                model.Roles = allNonAdminRoles.Select(role => new RoleViewModel
                {
                    Id = role.Id,
                    Name = role.Name,
                    IsSelected = userRoles.Contains(role.Name)
                }).ToList();
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                return NotFound();
            }

            try
            {
                // Cập nhật trạng thái
                user.Status = model.Status;
                user.EmailConfirmed = model.EmailConfirmed;

                // Cập nhật thông tin user
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    foreach (var error in updateResult.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                    ViewBag.Statuses = new List<string> { "Đang hoạt động", "Tạm khóa" };
                    // Nếu cập nhật lỗi, cần tải lại danh sách vai trò cho view
                    var userRoles = await _userManager.GetRolesAsync(user);
                    var allNonAdminRoles = _roleManager.Roles.Where(r => r.Name != "Admin").ToList();
                    model.Roles = allNonAdminRoles.Select(role => new RoleViewModel
                    {
                        Id = role.Id,
                        Name = role.Name,
                        IsSelected = userRoles.Contains(role.Name)
                    }).ToList();
                    return View(model);
                }

                // Cập nhật vai trò (chỉ Customer và Manager)
                var currentRoles = await _userManager.GetRolesAsync(user);
                // Các vai trò được chọn từ form, loại bỏ "Admin" nếu có ai đó cố tình gửi lên
                var selectedRoles = model.Roles.Where(r => r.IsSelected && r.Name != "Admin").Select(r => r.Name).ToList();

                // Các vai trò hiện tại của user, nhưng chỉ Customer và Manager
                var currentCustomerManagerRoles = currentRoles.Where(r => r == "Customer" || r == "Manager").ToList();

                // Các vai trò cần thêm: có trong selectedRoles nhưng không có trong currentCustomerManagerRoles
                var rolesToAdd = selectedRoles.Except(currentCustomerManagerRoles).ToList();
                if (rolesToAdd.Any())
                {
                    await _userManager.AddToRolesAsync(user, rolesToAdd);
                }

                // Các vai trò cần xóa: có trong currentCustomerManagerRoles nhưng không có trong selectedRoles
                var rolesToRemove = currentCustomerManagerRoles.Except(selectedRoles).ToList();
                if (rolesToRemove.Any())
                {
                    await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                }

                TempData["Message"] = "Cập nhật thông tin người dùng thành công!";
                TempData["MessageType"] = "success";

                // Redirect về trang Index sau khi lưu thành công
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Message"] = "Có lỗi xảy ra khi cập nhật: " + ex.Message;
                TempData["MessageType"] = "danger";
                ViewBag.Statuses = new List<string> { "Đang hoạt động", "Tạm khóa" };
                // Nếu có lỗi exception, cũng cần tải lại danh sách vai trò cho view
                var userRoles = await _userManager.GetRolesAsync(user);
                var allNonAdminRoles = _roleManager.Roles.Where(r => r.Name != "Admin").ToList();
                model.Roles = allNonAdminRoles.Select(role => new RoleViewModel
                {
                    Id = role.Id,
                    Name = role.Name,
                    IsSelected = userRoles.Contains(role.Name)
                }).ToList();
                return View(model);
            }
        }

    }

    public class UserViewModel
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Status { get; set; }
        public bool EmailConfirmed { get; set; }
        public List<string> Roles { get; set; }
    }

    public class UserDetailsViewModel : UserViewModel
    {
        public List<Order> Orders { get; set; }
    }

    public class UserEditViewModel
    {
        public string Id { get; set; }
        [Required(ErrorMessage = "Họ và tên không được để trống.")]
        public string FullName { get; set; }
        [Required(ErrorMessage = "Email không được để trống.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string Status { get; set; }
        public bool EmailConfirmed { get; set; }
        public List<RoleViewModel> Roles { get; set; } = new List<RoleViewModel>(); // Khởi tạo để tránh null reference
    }

    public class RoleViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsSelected { get; set; }
    }
}