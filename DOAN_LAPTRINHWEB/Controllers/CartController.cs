using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using DOAN_LAPTRINHWEB.Models;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json;

namespace DOAN_LAPTRINHWEB.Controllers
{
    public class CartController : Controller
    {
        private readonly HomeStylesDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private const string CartSessionKey = "Cart";

        public CartController(HomeStylesDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Cart
        public IActionResult Index()
        {
            var cart = GetCartFromSession();
            return View(cart);
        }

        // GET: Cart/AddToCartAjax
        [HttpGet]
        public async Task<IActionResult> AddToCartAjax(int productId, int quantity = 1)
        {
            // Find product in database
            var product = await _context.Products
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null)
            {
                return Json(new { success = false, message = "Sản phẩm không tồn tại." });
            }

            // Check stock
            if (product.Stock < quantity)
            {
                return Json(new
                {
                    success = false,
                    message = $"Chỉ còn {product.Stock} sản phẩm trong kho."
                });
            }

            // Get cart from session
            var cart = GetCartFromSession();

            // Check if product already exists in cart
            var existingItem = cart.Items.FirstOrDefault(item => item.ProductId == productId);
            if (existingItem != null)
            {
                // Check if new total quantity exceeds stock
                if (existingItem.Quantity + quantity > product.Stock)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Không thể thêm. Giỏ hàng của bạn đã có {existingItem.Quantity} sản phẩm và kho chỉ còn {product.Stock} sản phẩm."
                    });
                }

                // Update quantity if product already exists in cart
                existingItem.Quantity += quantity;
                existingItem.UpdateSubtotal();
            }
            else
            {
                // Add new product to cart
                var newItem = new CartItem
                {
                    ProductId = product.ProductId,
                    ProductName = product.Name,
                    Quantity = quantity,
                    Price = product.Price,
                    ImageUrl = product.ProductImages?.FirstOrDefault(img => img.IsPrimary == true)?.ImageUrl ?? "/images/no-image.jpg"
                };
                newItem.UpdateSubtotal();
                cart.Items.Add(newItem);
            }

            // Update cart totals
            cart.CalculateTotals();

            // Save cart to session
            SaveCartToSession(cart);

            return Json(new
            {
                success = true,
                message = $"Đã thêm {product.Name} vào giỏ hàng.",
                cartCount = cart.TotalItems
            });
        }

        // POST: Cart/AddToCart
        [HttpPost]
        public async Task<IActionResult> AddToCart(int productId, int quantity = 1)
        {
            // Find product in database
            var product = await _context.Products
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.ProductId == productId);

            if (product == null)
            {
                return Json(new { success = false, message = "Sản phẩm không tồn tại." });
            }

            // Check stock
            if (product.Stock < quantity)
            {
                return Json(new
                {
                    success = false,
                    message = $"Chỉ còn {product.Stock} sản phẩm trong kho."
                });
            }

            // Get cart from session
            var cart = GetCartFromSession();

            // Check if product already exists in cart
            var existingItem = cart.Items.FirstOrDefault(item => item.ProductId == productId);
            if (existingItem != null)
            {
                // Check if new total quantity exceeds stock
                if (existingItem.Quantity + quantity > product.Stock)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Không thể thêm. Giỏ hàng của bạn đã có {existingItem.Quantity} sản phẩm và kho chỉ còn {product.Stock} sản phẩm."
                    });
                }

                // Update quantity if product already exists in cart
                existingItem.Quantity += quantity;
                existingItem.UpdateSubtotal();
            }
            else
            {
                // Add new product to cart
                var newItem = new CartItem
                {
                    ProductId = product.ProductId,
                    ProductName = product.Name,
                    Quantity = quantity,
                    Price = product.Price,
                    ImageUrl = product.ProductImages?.FirstOrDefault(img => img.IsPrimary == true)?.ImageUrl ?? "/images/no-image.jpg"
                };
                newItem.UpdateSubtotal();
                cart.Items.Add(newItem);
            }

            // Update cart totals
            cart.CalculateTotals();

            // Save cart to session
            SaveCartToSession(cart);

            return Json(new
            {
                success = true,
                message = $"Đã thêm {product.Name} vào giỏ hàng.",
                cartCount = cart.TotalItems
            });
        }

        // POST: Cart/UpdateQuantity
        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int productId, int quantity)
        {
            if (quantity < 1)
            {
                return Json(new { success = false, message = "Số lượng phải lớn hơn 0" });
            }

            var cart = GetCartFromSession();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

            if (item == null)
            {
                return Json(new { success = false, message = "Sản phẩm không tồn tại trong giỏ hàng" });
            }

            // Check stock availability
            var product = await _context.Products.FindAsync(productId);
            if (product == null || product.Stock < quantity)
            {
                return Json(new
                {
                    success = false,
                    message = $"Chỉ còn {product?.Stock ?? 0} sản phẩm trong kho."
                });
            }

            item.Quantity = quantity;
            item.UpdateSubtotal();

            cart.CalculateTotals();
            SaveCartToSession(cart);

            return Json(new
            {
                success = true,
                message = "Đã cập nhật số lượng",
                subtotal = item.Subtotal,
                total = cart.TotalAmount,
                cartCount = cart.TotalItems
            });
        }

        // POST: Cart/RemoveItem
        [HttpPost]
        public IActionResult RemoveItem(int productId)
        {
            var cart = GetCartFromSession();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

            if (item != null)
            {
                cart.Items.Remove(item);
                cart.CalculateTotals();
                SaveCartToSession(cart);
            }

            return Json(new
            {
                success = true,
                message = "Đã xóa sản phẩm khỏi giỏ hàng",
                cartCount = cart.TotalItems,
                total = cart.TotalAmount
            });
        }

        // GET: Cart/Checkout
        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            var cart = GetCartFromSession();

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
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product == null || product.Stock < item.Quantity)
                {
                    TempData["ErrorMessage"] = $"Sản phẩm {item.ProductName} chỉ còn {product?.Stock ?? 0} trong kho.";
                    return RedirectToAction("Index");
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

        // POST: Cart/Checkout
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutViewModel model)
        {
            // Use a transaction to ensure database consistency
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Restore Cart from session as it cannot be bound from form
                model.Cart = GetCartFromSession();

                // Get current user
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy thông tin người dùng. Vui lòng đăng nhập lại.";
                    return RedirectToAction("Index", "Home");
                }

                // Restore other required fields
                model.User = user;
                model.UserId = user.Id;
                model.Order = new Order();

                if (model.Cart == null || model.Cart.Items.Count == 0)
                {
                    TempData["ErrorMessage"] = "Giỏ hàng của bạn đang trống";
                    return RedirectToAction("Index");
                }

                // Check stock availability again before processing order
                bool stockAvailable = true;
                string outOfStockMessage = "";

                foreach (var item in model.Cart.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null || product.Stock < item.Quantity)
                    {
                        stockAvailable = false;
                        outOfStockMessage = $"Sản phẩm {item.ProductName} chỉ còn {product?.Stock ?? 0} trong kho.";
                        break;
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
                        .Where(a => a.UserId == user.Id)
                        .ToListAsync();
                    ViewBag.Addresses = addresses;

                    return View(model);
                }

                // Create new order
                var order = new Order
                {
                    UserId = user.Id,
                    OrderStatus = "Chờ xác nhận",
                    TotalAmount = model.Cart.TotalAmount,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // Add order items and update product stock
                foreach (var item in model.Cart.Items)
                {
                    // Create order item
                    var orderItem = new OrderItem
                    {
                        OrderId = order.OrderId,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        Price = item.Price
                    };
                    _context.OrderItems.Add(orderItem);

                    // Update product stock
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.Stock -= item.Quantity;
                        _context.Products.Update(product);
                    }
                }
                await _context.SaveChangesAsync();

                // Save shipping address
                if (model.Address != null)
                {
                    var address = new Address
                    {
                        UserId = user.Id,
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
                    Amount = model.Cart.TotalAmount,
                    PaymentStatus = model.PaymentMethod == "COD" ? "Chờ thanh toán" : "Đang xử lý",
                    PaidAt = DateTime.Now
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                // Commit transaction
                await transaction.CommitAsync();

                // Clear cart after successful order
                HttpContext.Session.Remove(CartSessionKey);

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

                model.Cart = GetCartFromSession();

                // Ensure other properties are set when error occurs
                var user = await _userManager.GetUserAsync(User);
                model.User = user;
                model.UserId = user?.Id;
                model.Order = new Order();

                // Pass addresses to ViewBag for dropdown if needed
                var addresses = await _context.Addresses
                    .Where(a => a.UserId == user.Id)
                    .ToListAsync();
                ViewBag.Addresses = addresses;

                return View(model);
            }
        }

        // GET: Cart/OrderConfirmation/5
        [Authorize]
        public async Task<IActionResult> OrderConfirmation(int orderId)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.ProductImages)
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
        public IActionResult CartPartial()
        {
            var cart = GetCartFromSession();
            var cartItemCount = cart.TotalItems;
            return PartialView("_CartPartial", cartItemCount);
        }

        // Helper methods
        private Cart GetCartFromSession()
        {
            var cartJson = HttpContext.Session.GetString(CartSessionKey);
            if (string.IsNullOrEmpty(cartJson))
            {
                return new Cart();
            }
            return JsonConvert.DeserializeObject<Cart>(cartJson);
        }

        private void SaveCartToSession(Cart cart)
        {
            var cartJson = JsonConvert.SerializeObject(cart);
            HttpContext.Session.SetString(CartSessionKey, cartJson);
        }
    }
}