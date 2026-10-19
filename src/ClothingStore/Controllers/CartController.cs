using System.Security.Claims;
using ClothingStore.Filters;
using ClothingStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClothingStore.Controllers;

[Authorize, ShoppersOnly]
public class CartController : Controller
{
    private readonly CartService _cart;

    public CartController(CartService cart) => _cart = cart;

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index() => View(await _cart.GetCartAsync(UserId));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int productId, string? size, int quantity = 1, string? returnUrl = null, string? color = null)
    {
        var error = await _cart.AddAsync(UserId, productId, size, quantity, color);
        if (error != null) TempData["Error"] = error;
        else TempData["Success"] = "Item added to your cart.";

        if (Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }

    // Reached when an anonymous visitor clicks "Add to cart": the [Authorize] filter
    // sends them to the login page first, then back here to finish adding the item.
    [HttpGet]
    public async Task<IActionResult> AddAfterLogin(int productId, string? size, int quantity = 1, string? color = null)
    {
        var error = await _cart.AddAsync(UserId, productId, size, quantity, color);
        if (error != null) TempData["Error"] = error;
        else TempData["Success"] = "Item added to your cart.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, int quantity)
    {
        var error = await _cart.UpdateQuantityAsync(UserId, id, quantity);
        if (error != null) TempData["Error"] = error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id)
    {
        await _cart.RemoveAsync(UserId, id);
        return RedirectToAction(nameof(Index));
    }
}
