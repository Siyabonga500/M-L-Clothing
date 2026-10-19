using ClothingStore.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ClothingStore.Filters;

/// <summary>Sends admins and drivers to their own dashboards: they don't shop.</summary>
public class ShoppersOnlyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true || Roles.IsShopper(user)) return;

        if (context.Controller is Controller c)
            c.TempData["Info"] = "Cart, checkout and orders are for customers only.";

        context.Result = user.IsInRole(Roles.Admin)
            ? new RedirectToActionResult("Index", "Dashboard", new { area = "Admin" })
            : new RedirectToActionResult("Index", "Deliveries", new { area = "Driver" });
    }
}
