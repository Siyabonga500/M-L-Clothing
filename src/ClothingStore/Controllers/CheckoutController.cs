using System.Security.Claims;
using ClothingStore.Data;
using ClothingStore.Filters;
using ClothingStore.Models;
using ClothingStore.Services;
using ClothingStore.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace ClothingStore.Controllers;

[Authorize, ShoppersOnly]
public class CheckoutController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly CartService _cart;
    private readonly OrderService _orders;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly StripeSettings _stripe;
    private readonly StoreSettings _store;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(ApplicationDbContext db, CartService cart, OrderService orders,
        UserManager<ApplicationUser> userManager, IOptions<StripeSettings> stripe, IOptions<StoreSettings> store,
        ILogger<CheckoutController> logger)
    {
        _db = db;
        _cart = cart;
        _orders = orders;
        _userManager = userManager;
        _stripe = stripe.Value;
        _store = store.Value;
        _logger = logger;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var cart = await _cart.GetCartAsync(UserId);
        if (!cart.Items.Any()) return RedirectToAction("Index", "Cart");

        var user = await _userManager.GetUserAsync(User);
        var address = string.Join(", ", new[] { user!.HomeAddress, user.City, user.Province, user.PostalCode }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

        return View(new CheckoutViewModel
        {
            Cart = cart,
            RecipientName = user.FullName,
            ShippingAddress = address,
            Latitude = user.HomeLatitude,
            Longitude = user.HomeLongitude,
            Phone = user.PhoneNumber
        });
    }

    // Creates a pending order and sends the customer to Stripe's hosted payment page.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(CheckoutViewModel model)
    {
        var cart = await _cart.GetCartAsync(UserId);
        model.Cart = cart;
        if (!cart.Items.Any()) return RedirectToAction("Index", "Cart");

        if (model.Fulfilment == FulfilmentMethod.Delivery)
        {
            if (string.IsNullOrWhiteSpace(model.ShippingAddress))
                ModelState.AddModelError(nameof(model.ShippingAddress), "Enter your delivery address.");
            if (model.Latitude is null or < -90 or > 90 || model.Longitude is null or < -180 or > 180)
                ModelState.AddModelError(nameof(model.ShippingAddress), "Pick your delivery location on the map.");
        }
        if (!ModelState.IsValid) return View(model);

        if (string.IsNullOrWhiteSpace(_stripe.SecretKey) || _stripe.SecretKey.Contains("REPLACE_ME"))
        {
            TempData["Error"] = "Payments are not set up yet: add your Stripe secret key to appsettings.json (Stripe:SecretKey).";
            return RedirectToAction("Index", "Cart");
        }

        var outOfStock = cart.Items.FirstOrDefault(i =>
            !i.Product!.IsActive || i.Quantity > (i.Product.FindSize(i.Size)?.Quantity ?? 0));
        if (outOfStock != null)
        {
            TempData["Error"] = $"\"{outOfStock.Product!.Name}\" (size {outOfStock.Size}) is no longer available in that quantity.";
            return RedirectToAction("Index", "Cart");
        }

        var order = new Order
        {
            UserId = UserId,
            RecipientName = model.RecipientName,
            Fulfilment = model.Fulfilment,
            ShippingAddress = model.Fulfilment == FulfilmentMethod.Collection
                ? $"Collect at store: {_store.Address}"
                : model.ShippingAddress!,
            Phone = model.Phone,
            Total = cart.Total,
            Items = cart.Items.Select(i => new OrderItem
            {
                ProductId = i.ProductId,
                ProductName = i.Product!.Name,
                Size = i.Size,
                UnitPrice = i.Product.Price,
                Quantity = i.Quantity
            }).ToList()
        };
        if (order.IsDelivery)
        {
            order.DeliveryLatitude = model.Latitude;
            order.DeliveryLongitude = model.Longitude;
            var (km, minutes) = Geo.Estimate(_store.Latitude, _store.Longitude,
                model.Latitude!.Value, model.Longitude!.Value, _store.AverageSpeedKmh);
            order.DistanceKm = km;
            order.EstimatedMinutes = minutes;
        }
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var options = new SessionCreateOptions
        {
            Mode = "payment",
            CustomerEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name,
            ClientReferenceId = order.Id.ToString(),
            Metadata = new Dictionary<string, string> { ["orderId"] = order.Id.ToString() },
            SuccessUrl = $"{baseUrl}/Checkout/Success?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{baseUrl}/Checkout/Cancel?orderId={order.Id}",
            LineItems = order.Items.Select(i => new SessionLineItemOptions
            {
                Quantity = i.Quantity,
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = _stripe.Currency,
                    UnitAmount = (long)Math.Round(i.UnitPrice * 100m),
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = string.IsNullOrEmpty(i.Size) ? i.ProductName : $"{i.ProductName} (Size {i.Size})"
                    }
                }
            }).ToList()
        };

        try
        {
            var session = await new SessionService().CreateAsync(options);
            order.StripeSessionId = session.Id;
            await _db.SaveChangesAsync();
            return Redirect(session.Url);
        }
        catch (Exception ex) when (ex is StripeException or HttpRequestException)
        {
            _logger.LogError(ex, "Stripe checkout session creation failed for order {OrderId}", order.Id);
            order.Status = OrderStatus.Cancelled;
            await _db.SaveChangesAsync();
            TempData["Error"] = "Payment could not be started: " + ex.Message;
            return RedirectToAction("Index", "Cart");
        }
    }

    // Stripe redirects here after a successful payment. We confirm with Stripe before trusting it.
    [HttpGet]
    public async Task<IActionResult> Success(string session_id)
    {
        if (string.IsNullOrEmpty(session_id)) return RedirectToAction("Index", "Home");

        var order = await _db.Orders.FirstOrDefaultAsync(o => o.StripeSessionId == session_id && o.UserId == UserId);
        if (order == null) return NotFound();

        try
        {
            var session = await new SessionService().GetAsync(session_id);
            if (session.PaymentStatus == "paid")
                order = await _orders.MarkPaidAsync(session_id, session.PaymentIntentId);
        }
        catch (Exception ex) when (ex is StripeException or HttpRequestException)
        {
            _logger.LogError(ex, "Could not verify Stripe session {SessionId}", session_id);
        }

        return View(order);
    }

    [HttpGet]
    public async Task<IActionResult> Cancel(int orderId)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == UserId);
        if (order != null && order.Status == OrderStatus.PendingPayment)
        {
            order.Status = OrderStatus.Cancelled;
            await _db.SaveChangesAsync();
        }
        TempData["Error"] = "Payment was cancelled. Your cart is still saved.";
        return RedirectToAction("Index", "Cart");
    }

    // Optional: Stripe webhook so orders are marked paid even if the customer closes the browser.
    // Point a Stripe webhook at /Checkout/Webhook for the "checkout.session.completed" event.
    [AllowAnonymous, HttpPost, IgnoreAntiforgeryToken]
    public async Task<IActionResult> Webhook()
    {
        if (string.IsNullOrEmpty(_stripe.WebhookSecret)) return NotFound();

        var json = await new StreamReader(Request.Body).ReadToEndAsync();
        try
        {
            var stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], _stripe.WebhookSecret);
            if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted &&
                stripeEvent.Data.Object is Session session &&
                session.PaymentStatus == "paid")
            {
                await _orders.MarkPaidAsync(session.Id, session.PaymentIntentId);
            }
            return Ok();
        }
        catch (Exception ex) when (ex is StripeException or Newtonsoft.Json.JsonException or NullReferenceException)
        {
            // Bad signature or a payload that isn't a Stripe event.
            _logger.LogWarning(ex, "Invalid Stripe webhook");
            return BadRequest();
        }
    }
}
