using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using DOAN_LAPTRINHWEB.Models;
using Microsoft.AspNetCore.Authorization;
using DOAN_LAPTRINHWEB.Services;
using System.Text.Json;

namespace DOAN_LAPTRINHWEB.Controllers
{
    [Authorize]
    public class VnPaymentController : Controller
    {
        private readonly HomeStylesDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly CartService _cartService;
        private readonly IConfiguration _configuration;

        public VnPaymentController(
            HomeStylesDbContext context,
            UserManager<ApplicationUser> userManager,
            CartService cartService,
            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _cartService = cartService;
            _configuration = configuration;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePayment([FromBody] CheckoutSessionData checkoutData)
        {
            try
            {
                // Lưu thông tin thanh toán vào session để sử dụng sau khi thanh toán
                var sessionData = JsonSerializer.Serialize(checkoutData);
                HttpContext.Session.SetString("CheckoutSessionData", sessionData);

                // Lấy thông tin giỏ hàng
                var userId = await _cartService.GetCartUserIdAsync();
                var cart = await _cartService.GetCartAsync(userId);

                if (cart.Items.Count == 0)
                {
                    return Json(new { success = false, message = "Giỏ hàng của bạn đang trống" });
                }

                // Tạo đối tượng VnPayLibrary
                var vnpay = new VnPayLibrary(_configuration);

                // Lấy thông tin từ cấu hình
                var vnpTmnCode = _configuration["Vnpay:TmnCode"];
                var vnpHashSecret = _configuration["Vnpay:HashSecret"];
                var vnpUrl = _configuration["Vnpay:BaseUrl"];
                var vnpReturnUrl = $"{Request.Scheme}://{Request.Host}/VnPayment/PaymentCallback";

                // Tạo thông tin cần thiết cho VNPAY
                vnpay.AddRequestData("vnp_Version", _configuration["Vnpay:Version"]);
                vnpay.AddRequestData("vnp_Command", _configuration["Vnpay:Command"]);
                vnpay.AddRequestData("vnp_TmnCode", vnpTmnCode);

                // Sửa lại định dạng số tiền để đảm bảo đúng (không có phần thập phân)
                var amountInt = (long)(cart.TotalAmount * 100);
                vnpay.AddRequestData("vnp_Amount", amountInt.ToString());

                // Sử dụng định dạng ngày tháng chính xác
                vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
                vnpay.AddRequestData("vnp_CurrCode", _configuration["Vnpay:CurrCode"]);
                vnpay.AddRequestData("vnp_IpAddr", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");
                vnpay.AddRequestData("vnp_Locale", _configuration["Vnpay:Locale"]);

                // Tạo orderId để dễ nhận diện
                var orderId = DateTime.Now.Ticks.ToString();
                vnpay.AddRequestData("vnp_OrderInfo", $"Thanh toan don hang {orderId}");
                vnpay.AddRequestData("vnp_OrderType", "other"); // Sửa thành "other" thay vì "billpayment"
                vnpay.AddRequestData("vnp_ReturnUrl", vnpReturnUrl);
                vnpay.AddRequestData("vnp_TxnRef", orderId);

                // Tạo URL thanh toán
                string paymentUrl = vnpay.CreateRequestUrl(vnpUrl, vnpHashSecret);

                // Ghi log URL để kiểm tra
                Console.WriteLine($"VNPAY Payment URL: {paymentUrl}");

                return Json(new { success = true, redirectUrl = paymentUrl });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CreatePayment: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePaymentDirect(CheckoutSessionData checkoutData)
        {
            try
            {
                // Lưu thông tin thanh toán vào session để sử dụng sau khi thanh toán
                var sessionData = JsonSerializer.Serialize(checkoutData);
                HttpContext.Session.SetString("CheckoutSessionData", sessionData);

                // Lấy thông tin giỏ hàng
                var userId = await _cartService.GetCartUserIdAsync();
                var cart = await _cartService.GetCartAsync(userId);

                if (cart.Items.Count == 0)
                {
                    TempData["ErrorMessage"] = "Giỏ hàng của bạn đang trống";
                    return RedirectToAction("Checkout", "Cart");
                }

                // Tạo đối tượng VnPayLibrary
                var vnpay = new VnPayLibrary(_configuration);

                // Lấy thông tin từ cấu hình
                var vnpTmnCode = _configuration["Vnpay:TmnCode"];
                var vnpHashSecret = _configuration["Vnpay:HashSecret"];
                var vnpUrl = _configuration["Vnpay:BaseUrl"];
                var vnpReturnUrl = $"{Request.Scheme}://{Request.Host}/VnPayment/PaymentCallback";

                // Tạo thông tin cần thiết cho VNPAY
                vnpay.AddRequestData("vnp_Version", _configuration["Vnpay:Version"]);
                vnpay.AddRequestData("vnp_Command", _configuration["Vnpay:Command"]);
                vnpay.AddRequestData("vnp_TmnCode", vnpTmnCode);

                // Số tiền (không có thập phân)
                var amountInt = (long)(cart.TotalAmount * 100);
                vnpay.AddRequestData("vnp_Amount", amountInt.ToString());

                vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
                vnpay.AddRequestData("vnp_CurrCode", _configuration["Vnpay:CurrCode"]);
                vnpay.AddRequestData("vnp_IpAddr", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");
                vnpay.AddRequestData("vnp_Locale", _configuration["Vnpay:Locale"]);

                var orderId = DateTime.Now.Ticks.ToString();
                vnpay.AddRequestData("vnp_OrderInfo", $"Thanh toan don hang {orderId}");
                vnpay.AddRequestData("vnp_OrderType", "other");
                vnpay.AddRequestData("vnp_ReturnUrl", vnpReturnUrl);
                vnpay.AddRequestData("vnp_TxnRef", orderId);

                // Tạo URL thanh toán
                string paymentUrl = vnpay.CreateRequestUrl(vnpUrl, vnpHashSecret);

                // Chuyển hướng đến trang thanh toán VNPAY
                return Redirect(paymentUrl);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra: " + ex.Message;
                return RedirectToAction("Checkout", "Cart");
            }
        }
        public async Task<IActionResult> PaymentCallback()
        {
            // Lấy thông tin từ VNPAY trả về
            string vnp_ResponseCode = HttpContext.Request.Query["vnp_ResponseCode"];
            string vnp_TransactionNo = HttpContext.Request.Query["vnp_TransactionNo"];
            string vnp_TxnRef = HttpContext.Request.Query["vnp_TxnRef"];
            string vnp_SecureHash = HttpContext.Request.Query["vnp_SecureHash"];
            string vnp_PayDate = HttpContext.Request.Query["vnp_PayDate"];
            string vnp_OrderInfo = HttpContext.Request.Query["vnp_OrderInfo"];
            string vnp_BankCode = HttpContext.Request.Query["vnp_BankCode"];
            string vnp_Amount = HttpContext.Request.Query["vnp_Amount"];

            // Kiểm tra response code
            bool isSuccessful = vnp_ResponseCode == "00";

            // Lấy thông tin session checkout
            string sessionDataStr = HttpContext.Session.GetString("CheckoutSessionData");
            if (string.IsNullOrEmpty(sessionDataStr))
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin thanh toán. Vui lòng thử lại.";
                return RedirectToAction("Checkout", "Cart");
            }

            var checkoutData = JsonSerializer.Deserialize<CheckoutSessionData>(sessionDataStr);
            var currentUser = await _userManager.GetUserAsync(User);

            // Khởi tạo transaction
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                if (!isSuccessful)
                {
                    TempData["ErrorMessage"] = "Thanh toán không thành công. Vui lòng thử lại.";
                    return RedirectToAction("Checkout", "Cart");
                }

                // Thực hiện xử lý đơn hàng tương tự như trong CartController.Checkout
                var userId = currentUser.Id;
                var cart = await _cartService.GetCartAsync(userId);

                // Kiểm tra tồn kho
                bool stockAvailable = true;
                string outOfStockMessage = "";

                foreach (var item in cart.Items)
                {
                    if (item.ProductVariantId.HasValue)
                    {
                        var variant = await _context.ProductVariants.FindAsync(item.ProductVariantId);
                        if (variant == null || variant.Stock < item.Quantity)
                        {
                            stockAvailable = false;
                            outOfStockMessage = $"Biến thể của sản phẩm {item.ProductName} không đủ số lượng trong kho.";
                            break;
                        }
                    }
                    else
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product == null || product.Stock < item.Quantity)
                        {
                            stockAvailable = false;
                            outOfStockMessage = $"Sản phẩm {item.ProductName} chỉ còn {product?.Stock ?? 0} trong kho.";
                            break;
                        }
                    }
                }

                if (!stockAvailable)
                {
                    TempData["ErrorMessage"] = outOfStockMessage;
                    return RedirectToAction("Checkout", "Cart");
                }

                // Xử lý địa chỉ giao hàng
                Address shippingAddress;

                if (checkoutData.SelectedAddressId > 0)
                {
                    // Sử dụng địa chỉ đã chọn
                    shippingAddress = await _context.Addresses
                        .FirstOrDefaultAsync(a => a.AddressId == checkoutData.SelectedAddressId && a.UserId == userId);

                    if (shippingAddress == null)
                    {
                        TempData["ErrorMessage"] = "Địa chỉ được chọn không hợp lệ.";
                        return RedirectToAction("Checkout", "Cart");
                    }
                }
                else
                {
                    // Tạo địa chỉ mới
                    shippingAddress = new Address
                    {
                        UserId = currentUser.Id,
                        FullName = checkoutData.Address.FullName,
                        Phone = checkoutData.Address.Phone,
                        Street = checkoutData.Address.Street,
                        City = checkoutData.Address.City,
                        State = checkoutData.Address.State ?? "",
                        PostalCode = checkoutData.Address.PostalCode ?? "",
                        Country = checkoutData.Address.Country ?? "Việt Nam",
                        IsDefault = false,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    _context.Addresses.Add(shippingAddress);
                    await _context.SaveChangesAsync(); // Lưu để có AddressId

                    // Lưu vào tài khoản nếu được yêu cầu
                    if (checkoutData.SaveAddressToAccount)
                    {
                        // Địa chỉ đã được lưu ở trên rồi
                        Console.WriteLine("Đã lưu địa chỉ vào tài khoản");
                    }
                }

                // Tạo order với ShippingAddressId
                var order = new Order
                {
                    UserId = currentUser.Id,
                    OrderStatus = "Chờ xác nhận",
                    TotalAmount = cart.TotalAmount,
                    ShippingAddressId = shippingAddress.AddressId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // Add order items và update stock
                foreach (var item in cart.Items)
                {
                    var orderItem = new OrderItem
                    {
                        OrderId = order.OrderId,
                        ProductId = item.ProductId,
                        ProductVariantId = item.ProductVariantId,
                        Quantity = item.Quantity,
                        Price = item.Price
                    };
                    _context.OrderItems.Add(orderItem);

                    // Update stock
                    if (item.ProductVariantId.HasValue)
                    {
                        var variant = await _context.ProductVariants.FindAsync(item.ProductVariantId);
                        if (variant != null)
                        {
                            variant.Stock -= item.Quantity;
                            _context.ProductVariants.Update(variant);
                        }
                    }
                    else
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.Stock -= item.Quantity;
                            _context.Products.Update(product);
                        }
                    }
                }
                await _context.SaveChangesAsync();

                // Add payment information
                var payment = new Payment
                {
                    OrderId = order.OrderId,
                    PaymentMethod = "VnPay",
                    Amount = cart.TotalAmount,
                    PaymentStatus = "Đã thanh toán",
                    PaidAt = DateTime.Now,
                    TransactionId = vnp_TransactionNo,
                    PaymentDetails = JsonSerializer.Serialize(new
                    {
                        vnp_TxnRef,
                        vnp_BankCode,
                        vnp_PayDate,
                        vnp_OrderInfo
                    })
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                // Clear cart
                await _cartService.ClearCartAsync(userId);

                await transaction.CommitAsync();

                // Xóa session checkout data
                HttpContext.Session.Remove("CheckoutSessionData");

                TempData["SuccessMessage"] = "Thanh toán thành công!";
                return RedirectToAction("OrderConfirmation", "Cart", new { orderId = order.OrderId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["ErrorMessage"] = "Có lỗi xảy ra: " + ex.Message;
                return RedirectToAction("Checkout", "Cart");
            }
        }
    }
}