using System.Security.Claims;
using ClothingStore.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.ViewComponents;

public class CartCountViewComponent : ViewComponent
{
    private readonly CartService _cart;

    public CartCountViewComponent(CartService cart) => _cart = cart;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        var count = userId == null ? 0 : await _cart.CountAsync(userId);
        return View(count);
    }
}
