#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using DOAN_LAPTRINHWEB.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DOAN_LAPTRINHWEB.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IWebHostEnvironment webHostEnvironment)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _webHostEnvironment = webHostEnvironment;
        }

        public string Username { get; set; }
        public bool IsEmailConfirmed { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        [BindProperty]
        public IFormFile UploadAvatar { get; set; }

        public class InputModel
        {
            [Phone]
            [Display(Name = "Số điện thoại")]
            public string PhoneNumber { get; set; }

            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Họ và Tên là bắt buộc.")]
            [Display(Name = "Họ và Tên")]
            public string FullName { get; set; }

            [Display(Name = "Địa chỉ")]
            public string Address { get; set; }

            [DataType(DataType.Date)]
            [Display(Name = "Ngày sinh")]
            public DateTime? DateOfBirth { get; set; }

            [Display(Name = "Giới tính")]
            public string Gender { get; set; }

            [Display(Name = "Ảnh đại diện")]
            public string AvatarUrl { get; set; }
        }

        private async Task LoadAsync(ApplicationUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var email = await _userManager.GetEmailAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);

            Username = userName;
            IsEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);

            Input = new InputModel
            {
                Email = email,
                PhoneNumber = phoneNumber,
                FullName = user.FullName,
                Address = user.Address,
                DateOfBirth = user.DateOfBirth,
                Gender = user.Gender,
                AvatarUrl = user.AvatarUrl
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Không thể tải người dùng với ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Không thể tải người dùng với ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            if (UploadAvatar != null && UploadAvatar.Length > 0)
            {
                if (!string.IsNullOrEmpty(user.AvatarUrl) && user.AvatarUrl != "/images/avatars/default.png")
                {
                    string oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, user.AvatarUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "avatars");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(UploadAvatar.FileName);
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await UploadAvatar.CopyToAsync(fileStream);
                }

                user.AvatarUrl = "/images/avatars/" + uniqueFileName;
            }

            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            if (Input.PhoneNumber != phoneNumber)
            {
                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    StatusMessage = "Lỗi không mong muốn khi cố gắng đặt số điện thoại.";
                    return RedirectToPage();
                }
            }

            var email = await _userManager.GetEmailAsync(user);
            if (Input.Email != email)
            {
                var setEmailResult = await _userManager.SetEmailAsync(user, Input.Email);
                if (!setEmailResult.Succeeded)
                {
                    StatusMessage = "Lỗi không mong muốn khi cố gắng đặt email.";
                    return RedirectToPage();
                }
            }

            if (user.FullName != Input.FullName) user.FullName = Input.FullName;
            if (user.Address != Input.Address) user.Address = Input.Address;
            if (user.DateOfBirth != Input.DateOfBirth) user.DateOfBirth = Input.DateOfBirth;
            if (user.Gender != Input.Gender) user.Gender = Input.Gender;
            if (user.AvatarUrl != Input.AvatarUrl) Input.AvatarUrl = user.AvatarUrl;

            user.UpdatedAt = DateTime.UtcNow;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                StatusMessage = "Lỗi không mong muốn khi cố gắng cập nhật profile.";
                return RedirectToPage();
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Hồ sơ của bạn đã được cập nhật thành công.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSendVerificationEmailAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (string.IsNullOrWhiteSpace(Input.Email))
            {
                StatusMessage = "Email không được để trống để gửi email xác nhận.";
                return Page();
            }

            var userId = await _userManager.GetUserIdAsync(user);
            var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var callbackUrl = Url.Page(
                "/Account/ConfirmEmail",
                pageHandler: null,
                values: new { area = "Identity", userId = userId, code = code },
                protocol: Request.Scheme);

            // You should send the confirmation email here using your email service.

            StatusMessage = "Email xác nhận đã được gửi. Vui lòng kiểm tra email của bạn.";
            return RedirectToPage();
        }
    }
}
