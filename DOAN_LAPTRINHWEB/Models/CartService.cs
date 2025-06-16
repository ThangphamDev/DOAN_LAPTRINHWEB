using DOAN_LAPTRINHWEB.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DOAN_LAPTRINHWEB.Services
{
    public class CartService
    {
        private readonly HomeStylesDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<ApplicationUser> _userManager;
        private const string AnonymousCartIdKey = "AnonymousCartId";
        private const string AnonymousCartKey = "AnonymousCart";

        public CartService(HomeStylesDbContext context,
                            IHttpContextAccessor httpContextAccessor,
                            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _userManager = userManager;
        }

        // Kiểm tra xem user hiện tại có đăng nhập không
        private bool IsAuthenticated()
        {
            return _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
        }

        // Lấy ID giỏ hàng dựa trên user hoặc session
        public async Task<string> GetCartUserIdAsync()
        {
            // Kiểm tra nếu user đã đăng nhập
            if (IsAuthenticated())
            {
                var user = await _userManager.GetUserAsync(_httpContextAccessor.HttpContext.User);
                return user?.Id ?? Guid.NewGuid().ToString();
            }

            // Nếu chưa đăng nhập, tạo/lấy ID giỏ hàng tạm thời
            string anonymousCartId = _httpContextAccessor.HttpContext.Session.GetString(AnonymousCartIdKey);
            if (string.IsNullOrEmpty(anonymousCartId))
            {
                anonymousCartId = Guid.NewGuid().ToString();
                _httpContextAccessor.HttpContext.Session.SetString(AnonymousCartIdKey, anonymousCartId);
            }

            return anonymousCartId;
        }

        // Lấy giỏ hàng từ database hoặc session
        // Lấy giỏ hàng từ database hoặc session
        public async Task<Cart> GetCartAsync(string userId)
        {
            // Nếu user đã đăng nhập, lấy giỏ hàng từ database
            if (IsAuthenticated())
            {
                var cart = await _context.Carts
                    .Include(c => c.Items)
                        .ThenInclude(i => i.Product)
                            .ThenInclude(p => p.ProductImages)
                    .Include(c => c.Items)
                        .ThenInclude(i => i.ProductVariant)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                // Nếu giỏ hàng chưa tồn tại, tạo mới
                if (cart == null)
                {
                    cart = new Cart
                    {
                        UserId = userId,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        Items = new List<CartItem>()
                    };

                    _context.Carts.Add(cart);
                    await _context.SaveChangesAsync();
                }

                return cart; // Đã thêm câu lệnh return ở đây
            }
            // Nếu là user ẩn danh, lấy giỏ hàng từ session
            else
            {
                // Lấy giỏ hàng từ session
                var cartJson = _httpContextAccessor.HttpContext.Session.GetString(AnonymousCartKey);

                if (!string.IsNullOrEmpty(cartJson))
                {

                    // Phần xử lý cart từ session trong GetCartAsync
                    try
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };

                        // In ra thông tin JSON để debug
                        Console.WriteLine($"JSON từ session: {cartJson.Substring(0, Math.Min(100, cartJson.Length))}...");

                        var tempCart = JsonSerializer.Deserialize<JsonElement>(cartJson);

                        var cart = new Cart
                        {
                            UserId = userId,
                            CreatedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now,
                            Items = new List<CartItem>()
                        };

                        // Đảm bảo trích xuất đúng tên property từ JSON
                        if (tempCart.TryGetProperty("items", out var itemsElement) ||
                            tempCart.TryGetProperty("Items", out itemsElement))
                        {
                            if (itemsElement.ValueKind == JsonValueKind.Array)
                            {
                                Console.WriteLine($"Tìm thấy {itemsElement.GetArrayLength()} items trong JSON");
                                foreach (var item in itemsElement.EnumerateArray())
                                {
                                    try
                                    {
                                        int cartItemId = 0;
                                        int productId = 0;
                                        string productName = "";
                                        int quantity = 0;
                                        decimal price = 0;
                                        decimal subtotal = 0;
                                        string imageUrl = "";
                                        DateTime addedAt = DateTime.Now;

                                        // Trích xuất từng giá trị, xử lý khả năng property không tồn tại
                                        if (item.TryGetProperty("cartItemId", out var cidElement) ||
                                            item.TryGetProperty("CartItemId", out cidElement))
                                            cartItemId = cidElement.GetInt32();

                                        if (item.TryGetProperty("productId", out var pidElement) ||
                                            item.TryGetProperty("ProductId", out pidElement))
                                            productId = pidElement.GetInt32();

                                        if (item.TryGetProperty("productName", out var nameElement) ||
                                            item.TryGetProperty("ProductName", out nameElement))
                                            productName = nameElement.GetString();

                                        if (item.TryGetProperty("quantity", out var qtyElement) ||
                                            item.TryGetProperty("Quantity", out qtyElement))
                                            quantity = qtyElement.GetInt32();

                                        if (item.TryGetProperty("price", out var priceElement) ||
                                            item.TryGetProperty("Price", out priceElement))
                                            price = (decimal)priceElement.GetDouble();

                                        if (item.TryGetProperty("subtotal", out var subtotalElement) ||
                                            item.TryGetProperty("Subtotal", out subtotalElement))
                                            subtotal = (decimal)subtotalElement.GetDouble();

                                        if (item.TryGetProperty("imageUrl", out var imgElement) ||
                                            item.TryGetProperty("ImageUrl", out imgElement))
                                            imageUrl = imgElement.GetString();

                                        if (item.TryGetProperty("addedAt", out var dateElement) ||
                                            item.TryGetProperty("AddedAt", out dateElement))
                                            addedAt = dateElement.GetDateTime();

                                        var cartItem = new CartItem
                                        {
                                            CartItemId = cartItemId,
                                            ProductId = productId,
                                            ProductName = productName,
                                            Quantity = quantity,
                                            Price = price,
                                            Subtotal = subtotal,
                                            ImageUrl = imageUrl ?? "",
                                            AddedAt = addedAt
                                        };

                                        // Xử lý ProductVariantId nếu có
                                        if ((item.TryGetProperty("productVariantId", out var variantId) ||
                                             item.TryGetProperty("ProductVariantId", out variantId)) &&
                                            variantId.ValueKind != JsonValueKind.Null)
                                        {
                                            cartItem.ProductVariantId = variantId.GetInt32();
                                        }

                                        cart.Items.Add(cartItem);
                                        Console.WriteLine($"Đã thêm sản phẩm từ session: {cartItem.ProductName}");
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Error parsing cart item: {ex.Message}");
                                    }
                                }
                            }
                        }

                        // Nạp thông tin sản phẩm từ database
                        foreach (var item in cart.Items)
                        {
                            var product = await _context.Products
                                .Include(p => p.ProductImages)
                                .FirstOrDefaultAsync(p => p.ProductId == item.ProductId);

                            if (product != null)
                            {
                                item.Product = product;
                            }

                            if (item.ProductVariantId.HasValue)
                            {
                                var variant = await _context.ProductVariants
                                    .FirstOrDefaultAsync(v => v.VariantId == item.ProductVariantId.Value);

                                if (variant != null)
                                {
                                    item.ProductVariant = variant;
                                }
                            }
                        }

                        Console.WriteLine($"Đã trả về giỏ hàng từ session với {cart.Items.Count} sản phẩm");
                        return cart;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error deserializing cart: {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    }
                }

                // Tạo giỏ hàng mới nếu không tồn tại trong session
                var newCart = new Cart
                {
                    UserId = userId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    Items = new List<CartItem>()
                };

                SaveCartToSession(newCart);
                return newCart;
            }
        }

        // Lưu giỏ hàng vào session
        private void SaveCartToSession(Cart cart)
        {
            try
            {
                // In thông tin về giỏ hàng trước khi lưu
                Console.WriteLine($"Lưu giỏ hàng vào session: {cart.Items.Count} sản phẩm");
                foreach (var item in cart.Items)
                {
                    Console.WriteLine($"- Sản phẩm: {item.ProductName}, ID: {item.CartItemId}, SL: {item.Quantity}");
                }

                var simplifiedCart = new
                {
                    UserId = cart.UserId,
                    CreatedAt = cart.CreatedAt,
                    UpdatedAt = DateTime.Now,
                    Items = cart.Items.Select(item => new
                    {
                        CartItemId = item.CartItemId,
                        ProductId = item.ProductId,
                        ProductVariantId = item.ProductVariantId,
                        ProductName = item.ProductName,
                        Quantity = item.Quantity,
                        Price = (double)item.Price,
                        Subtotal = (double)item.Subtotal,
                        ImageUrl = item.ImageUrl,
                        AddedAt = item.AddedAt
                    }).ToList()
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var cartJson = JsonSerializer.Serialize(simplifiedCart, options);
                _httpContextAccessor.HttpContext.Session.SetString(AnonymousCartKey, cartJson);

                // Xác nhận đã lưu thành công
                Console.WriteLine("Đã lưu giỏ hàng vào session thành công");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SaveCartToSession: {ex.Message}");
            }
        }


        // Kiểm tra và hợp nhất giỏ hàng khi đăng nhập
        public async Task MergeCartsAsync(string anonymousId, string userId)
        {
            // Lấy giỏ hàng từ session
            var cartJson = _httpContextAccessor.HttpContext.Session.GetString(AnonymousCartKey);
            if (string.IsNullOrEmpty(cartJson))
            {
                return; // Không có giỏ hàng ẩn danh để hợp nhất
            }

            try
            {
                var anonymousCart = JsonSerializer.Deserialize<Cart>(cartJson);
                if (anonymousCart == null || anonymousCart.Items == null || !anonymousCart.Items.Any())
                {
                    return; // Giỏ hàng rỗng
                }

                // Lấy giỏ hàng của user đã đăng nhập từ database
                var userCart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                // Tạo giỏ hàng mới nếu chưa có
                if (userCart == null)
                {
                    userCart = new Cart
                    {
                        UserId = userId,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now,
                        Items = new List<CartItem>()
                    };

                    _context.Carts.Add(userCart);
                    await _context.SaveChangesAsync();
                }

                // Hợp nhất các sản phẩm từ giỏ hàng session
                foreach (var item in anonymousCart.Items)
                {
                    var existingItem = userCart.Items.FirstOrDefault(i =>
                        i.ProductId == item.ProductId && i.ProductVariantId == item.ProductVariantId);

                    if (existingItem != null)
                    {
                        // Cộng dồn số lượng nếu sản phẩm đã tồn tại
                        existingItem.Quantity += item.Quantity;
                        existingItem.Subtotal = existingItem.Price * existingItem.Quantity;
                        _context.CartItems.Update(existingItem);
                    }
                    else
                    {
                        // Tạo item mới nếu chưa tồn tại
                        var newItem = new CartItem
                        {
                            CartId = userCart.CartId,
                            ProductId = item.ProductId,
                            ProductVariantId = item.ProductVariantId,
                            ProductName = item.ProductName,
                            Quantity = item.Quantity,
                            Price = item.Price,
                            Subtotal = item.Subtotal,
                            ImageUrl = item.ImageUrl,
                            AddedAt = DateTime.Now
                        };

                        _context.CartItems.Add(newItem);
                    }
                }

                // Cập nhật giỏ hàng
                userCart.UpdatedAt = DateTime.Now;
                _context.Carts.Update(userCart);
                await _context.SaveChangesAsync();

                // Xóa giỏ hàng ẩn danh khỏi session
                _httpContextAccessor.HttpContext.Session.Remove(AnonymousCartKey);
                _httpContextAccessor.HttpContext.Session.Remove(AnonymousCartIdKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi hợp nhất giỏ hàng: {ex.Message}");
            }
        }

        // Thêm sản phẩm vào giỏ hàng
        public async Task<CartItem> AddToCartAsync(string userId, int productId, int quantity = 1, int? variantId = null)
        {
            try
            {
                // Lấy thông tin sản phẩm
                var product = await _context.Products
                    .Include(p => p.ProductImages)
                    .Include(p => p.ProductVariants)
                    .FirstOrDefaultAsync(p => p.ProductId == productId);

                if (product == null)
                {
                    Console.WriteLine($"Không tìm thấy sản phẩm có ID: {productId}");
                    return null;
                }

                // Thông tin sản phẩm
                string imageUrl = product.ProductImages?.FirstOrDefault(img => img.IsPrimary == true)?.ImageUrl ?? "/images/no-image.jpg";
                string productName = product.Name;
                decimal price = product.Price;

                // Kiểm tra tồn kho
                bool hasStock = false;

                // Debug thông tin tồn kho
                Console.WriteLine($"Sản phẩm: {product.Name}, Tồn kho: {product.Stock}, Số lượng yêu cầu: {quantity}");

                if (variantId.HasValue)
                {
                    var variant = product.ProductVariants.FirstOrDefault(v => v.VariantId == variantId.Value);
                    if (variant != null)
                    {
                        Console.WriteLine($"Biến thể: {variant.VariantId}, Tồn kho: {variant.Stock}");
                        if (variant.Stock >= quantity)
                        {
                            price += variant.AdditionalPrice ?? 0;
                            productName = $"{product.Name} ({variant.Size}, {variant.Color})";
                            hasStock = true;
                        }
                        else
                        {
                            Console.WriteLine($"Không đủ tồn kho cho biến thể. Yêu cầu: {quantity}, Hiện có: {variant.Stock}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Không tìm thấy biến thể với ID: {variantId}");
                    }
                }
                else
                {
                    if (product.Stock >= quantity)
                    {
                        hasStock = true;
                    }
                    else
                    {
                        Console.WriteLine($"Không đủ tồn kho. Yêu cầu: {quantity}, Hiện có: {product.Stock}");
                    }
                }

                // Bỏ qua kiểm tra tồn kho tạm thời nếu cần
                // hasStock = true;

                if (!hasStock)
                {
                    return null;
                }

                var cart = await GetCartAsync(userId);

                // Xác định có phải là user đăng nhập không
                if (IsAuthenticated())
                {
                    var existingItem = cart.Items.FirstOrDefault(i =>
                        i.ProductId == productId && i.ProductVariantId == variantId);

                    if (existingItem != null)
                    {
                        // Cập nhật số lượng nếu sản phẩm đã tồn tại
                        existingItem.Quantity += quantity;
                        existingItem.Subtotal = existingItem.Price * existingItem.Quantity;
                        _context.CartItems.Update(existingItem);
                        await _context.SaveChangesAsync();
                        return existingItem;
                    }
                    else
                    {
                        // Thêm sản phẩm mới vào giỏ hàng
                        var newItem = new CartItem
                        {
                            CartId = cart.CartId,
                            ProductId = productId,
                            ProductVariantId = variantId,
                            ProductName = productName,
                            Quantity = quantity,
                            Price = price,
                            Subtotal = price * quantity,
                            ImageUrl = imageUrl,
                            AddedAt = DateTime.Now
                        };

                        cart.Items.Add(newItem);
                        _context.CartItems.Add(newItem);
                        await _context.SaveChangesAsync();
                        return newItem;
                    }
                }
                else
                {
                    // Xử lý cho user ẩn danh - lưu vào session
                    var existingItem = cart.Items.FirstOrDefault(i =>
                        i.ProductId == productId &&
                        ((i.ProductVariantId == null && variantId == null) ||
                         (i.ProductVariantId == variantId)));

                    if (existingItem != null)
                    {
                        // Cập nhật số lượng nếu sản phẩm đã tồn tại
                        existingItem.Quantity += quantity;
                        existingItem.Subtotal = existingItem.Price * existingItem.Quantity;
                        SaveCartToSession(cart);
                        return existingItem;
                    }
                    else
                    {
                        // Thêm sản phẩm mới vào giỏ hàng
                        var newItem = new CartItem
                        {
                            CartItemId = Math.Abs(Guid.NewGuid().GetHashCode()),
                            ProductId = productId,
                            ProductVariantId = variantId,
                            ProductName = productName,
                            Quantity = quantity,
                            Price = price,
                            Subtotal = price * quantity,
                            ImageUrl = imageUrl,
                            AddedAt = DateTime.Now
                        };

                        cart.Items.Add(newItem);
                        SaveCartToSession(cart);
                        return newItem;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AddToCartAsync: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        // Cập nhật số lượng sản phẩm
        public async Task<bool> UpdateQuantityAsync(string userId, int cartItemId, int quantity)
        {
            if (quantity <= 0)
                return false;

            var cart = await GetCartAsync(userId);
            var item = cart.Items.FirstOrDefault(i => i.CartItemId == cartItemId);

            if (item == null)
                return false;

            // Kiểm tra tồn kho
            bool stockAvailable = false;

            if (item.ProductVariantId.HasValue)
            {
                var variant = await _context.ProductVariants.FindAsync(item.ProductVariantId.Value);
                stockAvailable = (variant != null && variant.Stock >= quantity);
            }
            else
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                stockAvailable = (product != null && product.Stock >= quantity);
            }

            if (!stockAvailable)
                return false;

            // Cập nhật số lượng
            item.Quantity = quantity;
            item.Subtotal = item.Price * quantity;

            // Xử lý theo loại user
            if (IsAuthenticated())
            {
                // Cập nhật giỏ hàng trong database
                cart.UpdatedAt = DateTime.Now;
                _context.CartItems.Update(item);
                _context.Carts.Update(cart);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Cập nhật giỏ hàng trong session
                cart.UpdatedAt = DateTime.Now;
                SaveCartToSession(cart);
            }

            return true;
        }

        // Xóa sản phẩm khỏi giỏ hàng
        // Xóa sản phẩm khỏi giỏ hàng
        public async Task<bool> RemoveFromCartAsync(string userId, int cartItemId)
        {
            try
            {
                var cart = await GetCartAsync(userId);
                // Tìm chính xác item cần xóa bằng ID
                var item = cart.Items.FirstOrDefault(i => i.CartItemId == cartItemId);

                if (item == null)
                {
                    Console.WriteLine($"Không tìm thấy item với id: {cartItemId}");
                    return false;
                }

                // Xử lý theo loại user
                if (IsAuthenticated())
                {
                    // Xóa khỏi database
                    _context.CartItems.Remove(item);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // Xóa sản phẩm khỏi danh sách
                    cart.Items.Remove(item);

                    // Tạo giỏ hàng mới hoàn toàn để tránh vấn đề tham chiếu
                    var newCart = new Cart
                    {
                        UserId = userId,
                        CreatedAt = cart.CreatedAt,
                        UpdatedAt = DateTime.Now,
                        Items = new List<CartItem>()
                    };

                    // Thêm lại các sản phẩm còn lại (ngoại trừ sản phẩm đã xóa)
                    foreach (var remainingItem in cart.Items)
                    {
                        newCart.Items.Add(new CartItem
                        {
                            CartItemId = remainingItem.CartItemId,
                            ProductId = remainingItem.ProductId,
                            ProductName = remainingItem.ProductName,
                            ProductVariantId = remainingItem.ProductVariantId,
                            Quantity = remainingItem.Quantity,
                            Price = remainingItem.Price,
                            Subtotal = remainingItem.Subtotal,
                            ImageUrl = remainingItem.ImageUrl,
                            AddedAt = remainingItem.AddedAt
                        });
                    }

                    // Xóa session cũ và lưu giỏ hàng mới
                    _httpContextAccessor.HttpContext.Session.Remove(AnonymousCartKey);
                    SaveCartToSession(newCart);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing item from cart: {ex.Message}");
                return false;
            }
        }

        // Xóa toàn bộ giỏ hàng
        public async Task ClearCartAsync(string userId)
        {
            if (IsAuthenticated())
            {
                // Xóa giỏ hàng từ database
                var cart = await GetCartAsync(userId);
                _context.CartItems.RemoveRange(cart.Items);
                cart.UpdatedAt = DateTime.Now;
                _context.Carts.Update(cart);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Xóa giỏ hàng session
                _httpContextAccessor.HttpContext.Session.Remove(AnonymousCartKey);

                // Tạo giỏ hàng mới và lưu lại vào session
                var newCart = new Cart
                {
                    UserId = userId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    Items = new List<CartItem>()
                };
                SaveCartToSession(newCart);
            }
        }
        // Thêm vào CartService
        public async Task<bool> RemoveFromCartByProductAsync(string userId, int productId, int? variantId = null)
        {
            try
            {
                var cart = await GetCartAsync(userId);
                var item = cart.Items.FirstOrDefault(i => i.ProductId == productId &&
                                                          ((i.ProductVariantId == null && variantId == null) ||
                                                           i.ProductVariantId == variantId));

                if (item == null)
                {
                    Console.WriteLine($"Không tìm thấy item với productId: {productId}");
                    return false;
                }

                // Xử lý theo loại user
                if (IsAuthenticated())
                {
                    // Xóa khỏi database
                    _context.CartItems.Remove(item);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    // Xóa khỏi session
                    cart.Items.Remove(item);
                    SaveCartToSession(cart);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing item from cart: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateQuantityByProductAsync(string userId, int productId, int quantity, int? variantId = null)
        {
            if (quantity <= 0)
                return false;

            var cart = await GetCartAsync(userId);
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId &&
                                                      ((i.ProductVariantId == null && variantId == null) ||
                                                       i.ProductVariantId == variantId));

            if (item == null)
                return false;

            // Kiểm tra tồn kho
            bool stockAvailable = false;

            if (item.ProductVariantId.HasValue)
            {
                var variant = await _context.ProductVariants.FindAsync(item.ProductVariantId.Value);
                stockAvailable = (variant != null && variant.Stock >= quantity);
            }
            else
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                stockAvailable = (product != null && product.Stock >= quantity);
            }

            if (!stockAvailable)
                return false;

            // Cập nhật số lượng
            item.Quantity = quantity;
            item.Subtotal = item.Price * quantity;

            // Xử lý theo loại user
            if (IsAuthenticated())
            {
                // Cập nhật giỏ hàng trong database
                cart.UpdatedAt = DateTime.Now;
                _context.CartItems.Update(item);
                _context.Carts.Update(cart);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Cập nhật giỏ hàng trong session
                cart.UpdatedAt = DateTime.Now;
                SaveCartToSession(cart);
            }

            return true;
        }

    }
}