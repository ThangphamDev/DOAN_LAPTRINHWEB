using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DOAN_LAPTRINHWEB.Models;
using Microsoft.AspNetCore.Authorization;
using DOAN_LAPTRINHWEB.Services;

namespace DOAN_LAPTRINHWEB.Controllers
{
    public class CartController : Controller
    {
        private readonly HomeStylesDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly CartService _cartService;

        public CartController(HomeStylesDbContext context,
                              UserManager<ApplicationUser> userManager,
                              CartService cartService)
        {
            _context = context;
            _userManager = userManager;
            _cartService = cartService;
        }

        // GET: Cart
        public async Task<IActionResult> Index()
        {
            var userId = await _cartService.GetCartUserIdAsync();
            var cart = await _cartService.GetCartAsync(userId);
            return View(cart);
        }

        // GET: Cart/AddToCartAjax
        [HttpGet]
        public async Task<IActionResult> AddToCartAjax(int productId, int quantity = 1, int? variantId = null)
        {
            var userId = await _cartService.GetCartUserIdAsync();

            // Thêm sản phẩm vào giỏ hàng
            var cartItem = await _cartService.AddToCartAsync(userId, productId, quantity, variantId);

            if (cartItem == null)
            {
                return Json(new { success = false, message = "Không thể thêm sản phẩm vào giỏ hàng. Vui lòng kiểm tra tồn kho." });
            }

            // Lấy thông tin giỏ hàng cập nhật
            var cart = await _cartService.GetCartAsync(userId);

            return Json(new
            {
                success = true,
                message = $"Đã thêm {cartItem.ProductName} vào giỏ hàng.",
                cartCount = cart.UniqueItemsCount
            });
        }

        // POST: Cart/AddToCart
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1, int? variantId = null)
        {
            return await AddToCartAjax(productId, quantity, variantId);
        }

        // POST: Cart/UpdateQuantity
        [HttpPost]
        public async Task<IActionResult> UpdateQuantityByProduct(int productId, int quantity, int? variantId = null)
        {
            if (quantity < 1)
            {
                return Json(new { success = false, message = "Số lượng phải lớn hơn 0" });
            }

            var userId = await _cartService.GetCartUserIdAsync();
            var success = await _cartService.UpdateQuantityByProductAsync(userId, productId, quantity, variantId);

            if (!success)
            {
                return Json(new { success = false, message = "Không thể cập nhật số lượng. Kiểm tra lại tồn kho." });
            }

            // Lấy thông tin giỏ hàng cập nhật
            var cart = await _cartService.GetCartAsync(userId);
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId &&
                                                      ((i.ProductVariantId == null && variantId == null) ||
                                                       i.ProductVariantId == variantId));

            return Json(new
            {
                success = true,
                message = "Đã cập nhật số lượng",
                subtotal = item?.Subtotal ?? 0,
                total = cart.TotalAmount,
                cartCount = cart.UniqueItemsCount
            });
        }

        // POST: Cart/RemoveItem
        [HttpPost]
        public async Task<IActionResult> RemoveItem(int cartItemId)
        {
            var userId = await _cartService.GetCartUserIdAsync();
            await _cartService.RemoveFromCartAsync(userId, cartItemId);

            // Lấy thông tin giỏ hàng cập nhật
            var cart = await _cartService.GetCartAsync(userId);

            return Json(new
            {
                success = true,
                message = "Đã xóa sản phẩm khỏi giỏ hàng",
                cartCount = cart.UniqueItemsCount,
                total = cart.TotalAmount
            });
        }

        // GET: Cart/Checkout
        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            var userId = await _cartService.GetCartUserIdAsync();
            var anonymousId = HttpContext.Session.GetString("AnonymousCartId");

            // Nếu trước đó user đã có giỏ hàng ẩn danh, hợp nhất với giỏ hàng user
            if (!string.IsNullOrEmpty(anonymousId))
            {
                var currentUser = await _userManager.GetUserAsync(User);
                await _cartService.MergeCartsAsync(anonymousId, currentUser.Id);
            }

            // Lấy giỏ hàng
            var cart = await _cartService.GetCartAsync(userId);

            if (cart.Items.Count == 0)
            {
                TempData["ErrorMessage"] = "Giỏ hàng của bạn đang trống";
                return RedirectToAction("Index");
            }

            // Get current user
            var user = await _userManager.GetUserAsync(User);

            // Check stock availability before proceeding to checkout
            foreach (var item in cart.Items)
            {
                // Kiểm tra tồn kho dựa trên biến thể hoặc sản phẩm
                if (item.ProductVariantId.HasValue)
                {
                    var variant = await _context.ProductVariants.FindAsync(item.ProductVariantId);
                    if (variant == null || variant.Stock < item.Quantity)
                    {
                        TempData["ErrorMessage"] = $"Biến thể của sản phẩm {item.ProductName} không đủ số lượng trong kho.";
                        return RedirectToAction("Index");
                    }
                }
                else
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null || product.Stock < item.Quantity)
                    {
                        TempData["ErrorMessage"] = $"Sản phẩm {item.ProductName} chỉ còn {product?.Stock ?? 0} trong kho.";
                        return RedirectToAction("Index");
                    }
                }
            }

            // Get user's saved addresses if available
            var addresses = await _context.Addresses
                .Where(a => a.UserId == user.Id)
                .ToListAsync();

            // Create checkout view model
            var checkoutViewModel = new CheckoutViewModel
            {
                Cart = cart,
                Address = addresses.FirstOrDefault() ?? new Address
                {
                    FullName = user.FullName,
                    Phone = user.PhoneNumber,
                    UserId = user.Id
                },
                User = user,
                UserId = user.Id,
                Order = new Order()
            };

            // Pass addresses to ViewBag for dropdown if needed
            ViewBag.Addresses = addresses;

            return View(checkoutViewModel);
        }

        // POST: Cart/Checkout - Phương thức này sẽ được chỉnh sửa để sử dụng giỏ hàng từ database thay vì session
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutViewModel model)
        {
            // Use a transaction to ensure database consistency
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Get current user and cart
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy thông tin người dùng. Vui lòng đăng nhập lại.";
                    return RedirectToAction("Index", "Home");
                }

                var userId = currentUser.Id;
                var cart = await _cartService.GetCartAsync(userId);

                // Restore model info
                model.Cart = cart;
                model.User = currentUser;
                model.UserId = userId;
                model.Order = new Order();

                if (cart.Items.Count == 0)
                {
                    TempData["ErrorMessage"] = "Giỏ hàng của bạn đang trống";
                    return RedirectToAction("Index");
                }

                // Check stock availability again before processing order
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
                    return RedirectToAction("Index");
                }

                // Validate model
                if (string.IsNullOrEmpty(model.Address?.FullName) ||
                    string.IsNullOrEmpty(model.Address?.Phone) ||
                    string.IsNullOrEmpty(model.Address?.Street) ||
                    string.IsNullOrEmpty(model.Address?.City) ||
                    string.IsNullOrEmpty(model.PaymentMethod))
                {
                    ModelState.AddModelError("", "Vui lòng điền đầy đủ thông tin giao hàng và phương thức thanh toán");

                    // Pass addresses to ViewBag for dropdown if needed
                    var addresses = await _context.Addresses
                        .Where(a => a.UserId == currentUser.Id)
                        .ToListAsync();
                    ViewBag.Addresses = addresses;

                    return View(model);
                }

                // Create new order
                var order = new Order
                {
                    UserId = currentUser.Id,
                    OrderStatus = "Chờ xác nhận",
                    TotalAmount = cart.TotalAmount,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // Add order items and update product stock
                foreach (var item in cart.Items)
                {
                    // Create order item
                    var orderItem = new OrderItem
                    {
                        OrderId = order.OrderId,
                        ProductId = item.ProductId,
                        ProductVariantId = item.ProductVariantId,
                        Quantity = item.Quantity,
                        Price = item.Price
                    };
                    _context.OrderItems.Add(orderItem);

                    // Update product stock based on whether item has variant or not
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

                // Save shipping address
                if (model.Address != null)
                {
                    var address = new Address
                    {
                        UserId = currentUser.Id,
                        FullName = model.Address.FullName,
                        Phone = model.Address.Phone,
                        Street = model.Address.Street,
                        City = model.Address.City,
                        State = model.Address.State ?? "",
                        PostalCode = model.Address.PostalCode ?? "",
                        Country = model.Address.Country ?? "Việt Nam"
                    };

                    _context.Addresses.Add(address);
                    await _context.SaveChangesAsync();
                }

                // Add payment information
                var payment = new Payment
                {
                    OrderId = order.OrderId,
                    PaymentMethod = model.PaymentMethod,
                    Amount = cart.TotalAmount,
                    PaymentStatus = model.PaymentMethod == "COD" ? "Chờ thanh toán" : "Đang xử lý",
                    PaidAt = DateTime.Now
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                // Clear cart after successful order
                await _cartService.ClearCartAsync(userId);

                // Commit transaction
                await transaction.CommitAsync();

                return RedirectToAction("OrderConfirmation", new { orderId = order.OrderId });
            }
            catch (Exception ex)
            {
                // Roll back transaction
                await transaction.RollbackAsync();

                // Log error details
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                Console.WriteLine($"Error in Checkout: {errorMessage}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                TempData["ErrorMessage"] = $"Có lỗi xảy ra khi xử lý đơn hàng: {errorMessage}";

                // Reload cart and user data for the view
                var currentUser = await _userManager.GetUserAsync(User);
                var cart = await _cartService.GetCartAsync(currentUser.Id);

                model.Cart = cart;
                model.User = currentUser;
                model.UserId = currentUser?.Id;
                model.Order = new Order();

                // Pass addresses to ViewBag for dropdown if needed
                var addresses = await _context.Addresses
                    .Where(a => a.UserId == currentUser.Id)
                    .ToListAsync();
                ViewBag.Addresses = addresses;

                return View(model);
            }
        }

        // GET: Cart/OrderConfirmation/5 - Phương thức này gần như không thay đổi
        [Authorize]
        public async Task<IActionResult> OrderConfirmation(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.ProductImages)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.ProductVariant)
                .Include(o => o.User)
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
            {
                return NotFound();
            }

            // Ensure order belongs to current user
            var currentUser = await _userManager.GetUserAsync(User);
            if (order.UserId != currentUser.Id)
            {
                return Forbid();
            }

            // Get shipping address for order
            var addresses = await _context.Addresses
                .Where(a => a.UserId == currentUser.Id)
                .OrderByDescending(a => a.AddressId)  // Get most recently added address
                .FirstOrDefaultAsync();

            // Get payment information
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.OrderId == orderId);

            var viewModel = new OrderConfirmationViewModel
            {
                Order = order,
                Address = addresses,
                Payment = payment,
                OrderItems = order.OrderItems.ToList()
            };

            return View(viewModel);
        }

        // GET: Cart/CartPartial (to display cart count in navbar)
        public async Task<IActionResult> CartPartial()
        {
            var userId = await _cartService.GetCartUserIdAsync();
            var cart = await _cartService.GetCartAsync(userId);
            var cartItemCount = cart.UniqueItemsCount;
            return PartialView("_CartPartial", cartItemCount);
        }
        [HttpPost]
        public async Task<IActionResult> RemoveItemByProduct(int productId, int? variantId = null)
        {
            var userId = await _cartService.GetCartUserIdAsync();
            var success = await _cartService.RemoveFromCartByProductAsync(userId, productId, variantId);

            if (!success)
            {
                return Json(new { success = false, message = "Không thể xóa sản phẩm" });
            }

            // Lấy thông tin giỏ hàng cập nhật
            var cart = await _cartService.GetCartAsync(userId);

            return Json(new
            {
                success = true,
                message = "Đã xóa sản phẩm khỏi giỏ hàng",
                cartCount = cart.UniqueItemsCount,
                total = cart.TotalAmount
            });
        }
    }
}