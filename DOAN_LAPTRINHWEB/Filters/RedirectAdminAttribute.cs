using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DOAN_LAPTRINHWEB.Filters
{
    public class RedirectAdminAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // Kiểm tra nếu người dùng đăng nhập là admin và không đang ở trong khu vực Admin
            if (context.HttpContext.User.Identity.IsAuthenticated &&
                context.HttpContext.User.IsInRole("Admin") &&
                !context.HttpContext.Request.RouteValues.ContainsKey("area"))
            {
                // Chuyển hướng admin về trang Dashboard trong area Admin
                context.Result = new RedirectToActionResult("Dashboard", "Admin", new { area = "Admin" });
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
