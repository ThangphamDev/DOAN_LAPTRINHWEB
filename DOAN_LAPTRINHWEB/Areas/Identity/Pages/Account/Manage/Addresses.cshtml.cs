using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DOAN_LAPTRINHWEB.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DOAN_LAPTRINHWEB.Areas.Identity.Pages.Account.Manage
{
    public class AddressesModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly HomeStylesDbContext _context;
        private readonly ILogger<AddressesModel> _logger;

        public AddressesModel(
            UserManager<ApplicationUser> userManager,
            HomeStylesDbContext context,
            ILogger<AddressesModel> logger)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
        }

        [TempData]
        public string StatusMessage { get; set; }

        public List<Address> Addresses { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Không thể tải thông tin người dùng với ID '{_userManager.GetUserId(User)}'.");
            }

            Addresses = await _context.Addresses
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.IsDefault)
                .ThenBy(a => a.FullName)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAddAsync(string fullName, string phone, string street, string city, string state, string country, string postalCode, bool isDefault = false)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Không thể tải thông tin người dùng với ID '{_userManager.GetUserId(User)}'.");
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(phone) ||
                string.IsNullOrWhiteSpace(street) || string.IsNullOrWhiteSpace(city))
            {
                StatusMessage = "Lỗi: Vui lòng điền đầy đủ thông tin bắt buộc (Họ tên, Số điện thoại, Địa chỉ, Thành phố).";
                await OnGetAsync();
                return Page();
            }

            var address = new Address
            {
                UserId = user.Id,
                FullName = fullName.Trim(),
                Phone = phone.Trim(),
                Street = street.Trim(),
                City = city.Trim(),
                State = state?.Trim() ?? string.Empty,
                Country = country?.Trim() ?? "Việt Nam",
                PostalCode = postalCode?.Trim() ?? string.Empty,
                IsDefault = isDefault
            };

            // Nếu đây là địa chỉ đầu tiên hoặc được đánh dấu là mặc định
            var isFirstAddress = !await _context.Addresses.AnyAsync(a => a.UserId == user.Id);
            if (isFirstAddress || isDefault)
            {
                // Đặt tất cả các địa chỉ khác thành không mặc định
                var existingAddresses = await _context.Addresses
                    .Where(a => a.UserId == user.Id)
                    .ToListAsync();

                foreach (var existingAddress in existingAddresses)
                {
                    existingAddress.IsDefault = false;
                }

                address.IsDefault = true;
            }

            try
            {
                _context.Addresses.Add(address);
                await _context.SaveChangesAsync();
                StatusMessage = "Đã thêm địa chỉ mới thành công.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thêm địa chỉ mới");
                StatusMessage = "Lỗi: Không thể thêm địa chỉ. Vui lòng thử lại.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEditAsync(int addressId, string fullName, string phone, string street, string city, string state, string country, string postalCode, bool isDefault = false)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Không thể tải thông tin người dùng với ID '{_userManager.GetUserId(User)}'.");
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(phone) ||
                string.IsNullOrWhiteSpace(street) || string.IsNullOrWhiteSpace(city))
            {
                StatusMessage = "Lỗi: Vui lòng điền đầy đủ thông tin bắt buộc (Họ tên, Số điện thoại, Địa chỉ, Thành phố).";
                await OnGetAsync();
                return Page();
            }

            var existingAddress = await _context.Addresses
                .FirstOrDefaultAsync(a => a.AddressId == addressId && a.UserId == user.Id);

            if (existingAddress == null)
            {
                StatusMessage = "Lỗi: Không tìm thấy địa chỉ.";
                return RedirectToPage();
            }

            // Cập nhật thông tin địa chỉ
            existingAddress.FullName = fullName.Trim();
            existingAddress.Phone = phone.Trim();
            existingAddress.Street = street.Trim();
            existingAddress.City = city.Trim();
            existingAddress.State = state?.Trim() ?? string.Empty;
            existingAddress.Country = country?.Trim() ?? "Việt Nam";
            existingAddress.PostalCode = postalCode?.Trim() ?? string.Empty;

            // Xử lý trạng thái mặc định
            if (isDefault && !existingAddress.IsDefault)
            {
                // Đặt tất cả địa chỉ khác thành không mặc định
                var otherAddresses = await _context.Addresses
                    .Where(a => a.UserId == user.Id && a.AddressId != addressId)
                    .ToListAsync();

                foreach (var address in otherAddresses)
                {
                    address.IsDefault = false;
                }

                existingAddress.IsDefault = true;
            }

            try
            {
                await _context.SaveChangesAsync();
                StatusMessage = "Đã cập nhật địa chỉ thành công.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi cập nhật địa chỉ");
                StatusMessage = "Lỗi: Không thể cập nhật địa chỉ. Vui lòng thử lại.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int addressId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Không thể tải thông tin người dùng với ID '{_userManager.GetUserId(User)}'.");
            }

            var address = await _context.Addresses
                .FirstOrDefaultAsync(a => a.AddressId == addressId && a.UserId == user.Id);

            if (address == null)
            {
                StatusMessage = "Lỗi: Không tìm thấy địa chỉ.";
                return RedirectToPage();
            }

            try
            {
                _context.Addresses.Remove(address);

                // Nếu địa chỉ bị xóa là địa chỉ mặc định và còn địa chỉ khác
                if (address.IsDefault)
                {
                    var remainingAddress = await _context.Addresses
                        .Where(a => a.UserId == user.Id && a.AddressId != addressId)
                        .FirstOrDefaultAsync();

                    if (remainingAddress != null)
                    {
                        remainingAddress.IsDefault = true;
                    }
                }

                await _context.SaveChangesAsync();
                StatusMessage = "Đã xóa địa chỉ thành công.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa địa chỉ");
                StatusMessage = "Lỗi: Không thể xóa địa chỉ. Vui lòng thử lại.";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSetDefaultAsync(int addressId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Không thể tải thông tin người dùng với ID '{_userManager.GetUserId(User)}'.");
            }

            try
            {
                // Đặt tất cả địa chỉ thành không mặc định
                var userAddresses = await _context.Addresses
                    .Where(a => a.UserId == user.Id)
                    .ToListAsync();

                foreach (var address in userAddresses)
                {
                    address.IsDefault = (address.AddressId == addressId);
                }

                await _context.SaveChangesAsync();
                StatusMessage = "Đã đặt địa chỉ mặc định thành công.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đặt địa chỉ mặc định");
                StatusMessage = "Lỗi: Không thể đặt địa chỉ mặc định. Vui lòng thử lại.";
            }

            return RedirectToPage();
        }
    }
}
