using System.Globalization;
using System.Net;
using ClothingStore.Data;
using ClothingStore.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClothingStore.Services;

/// <summary>
/// Everything that happens to an order after payment: drivers, QR codes, collection and emails.
/// </summary>
public class DeliveryService
{
    private readonly ApplicationDbContext _db;
    private readonly EmailService _email;
    private readonly UserManager<ApplicationUser> _users;
    private readonly StoreSettings _store;
    private readonly IConfiguration _config;

    public DeliveryService(ApplicationDbContext db, EmailService email, UserManager<ApplicationUser> users,
        IOptions<StoreSettings> store, IConfiguration config)
    {
        _db = db;
        _email = email;
        _users = users;
        _store = store.Value;
        _config = config;
    }

    public static string ScanUrl(string baseUrl, string token) => $"{baseUrl}/Scan?token={token}";

    public Task<Order?> LoadAsync(int id) =>
        _db.Orders.Include(o => o.User).Include(o => o.Driver).Include(o => o.Items)
            .Include(o => o.Events.OrderByDescending(e => e.CreatedAt))
            .FirstOrDefaultAsync(o => o.Id == id);

    public void AddEvent(Order order, string message) =>
        _db.OrderEvents.Add(new OrderEvent { OrderId = order.Id, Message = message });

    private void NewQr(Order order)
    {
        order.QrToken = QrService.NewToken();
        order.QrGeneratedAt = DateTime.UtcNow;
        order.QrExpiresAt = DateTime.UtcNow.AddHours(_store.QrValidHours);
        order.QrUsedAt = null;
    }

    private static string Date(DateTime utc) =>
        utc.ToLocalTime().ToString("dddd, d MMMM yyyy", CultureInfo.GetCultureInfo("en-ZA"));

    private string Enc(string s) => WebUtility.HtmlEncode(s);

    private EmailAttachment QrAttachment(Order order, string baseUrl) =>
        new($"order-{order.Id}-qr.png", QrService.Png(ScanUrl(baseUrl, order.QrToken!)), "image/png");

    // ---------- Admin actions ----------

    /// <summary>Assigns a driver. The delivery is accepted for the driver automatically.</summary>
    public async Task<string?> AssignDriverAsync(int orderId, string driverId, string baseUrl)
    {
        var order = await LoadAsync(orderId);
        if (order == null) return "Order not found.";
        if (!order.IsDelivery) return "This order is for collection, not delivery.";
        if (!order.IsPaid || order.IsCompleted) return "Only paid orders that aren't completed can be assigned.";

        var driver = await _users.FindByIdAsync(driverId);
        if (driver == null || !await _users.IsInRoleAsync(driver, Roles.Driver)) return "Driver not found.";

        var now = DateTime.UtcNow;
        order.DriverId = driver.Id;
        order.DriverAssignedAt = now;
        order.DeliveryWindowEnd = now.AddDays(_store.DeliveryDays);
        order.DepartedAt = null;
        order.Status = OrderStatus.DriverAssigned;
        NewQr(order);
        AddEvent(order, $"Driver {driver.FullName} assigned and accepted the delivery.");
        await _db.SaveChangesAsync();

        var orderUrl = $"{baseUrl}/Orders/Details/{order.Id}";
        var customer = order.User!;
        await _email.SendAsync(customer.Email!, $"Your order #{order.Id} will be delivered within {_store.DeliveryDays} days",
            EmailService.Layout(_store.Name, "Your order is on its way soon", $"""
                <p>Hi {Enc(customer.FirstName)},</p>
                <p>Good news! A driver has been assigned to your order <strong>#{order.Id}</strong>.</p>
                <p style="background:#e8f1ef;border-left:4px solid #183630;padding:12px">
                  Your order will be delivered within <strong>{_store.DeliveryDays} days from today</strong>
                  ({Date(now)} – {Date(order.DeliveryWindowEnd.Value)}).<br>
                  Please make sure you are available at your delivery address between these {_store.DeliveryDays} days.
                </p>
                <p><strong>Delivery address:</strong> {Enc(order.ShippingAddress)}<br>
                   <strong>Driver:</strong> {Enc(driver.FullName)}</p>
                <p>Your delivery QR code is attached and on your order page. When the driver arrives,
                   show it so they can scan it to confirm delivery. It is valid until {Date(order.QrExpiresAt!.Value)}.
                   Don't share it with anyone else.</p>
                <p><a href="{orderUrl}" style="background:#183630;color:#fff;padding:10px 18px;border-radius:6px;text-decoration:none">View my order &amp; QR code</a></p>
                """), QrAttachment(order, baseUrl));

        await _email.SendAsync(driver.Email!, $"New delivery assigned: order #{order.Id}",
            EmailService.Layout(_store.Name, "New delivery assigned", $"""
                <p>Hi {Enc(driver.FirstName)},</p>
                <p>Order <strong>#{order.Id}</strong> has been assigned to you and accepted automatically.
                   Deliver by {Date(order.DeliveryWindowEnd.Value)}.</p>
                <p><strong>Customer:</strong> {Enc(order.RecipientName)} ({Enc(order.Phone ?? "-")})<br>
                   <strong>Address:</strong> {Enc(order.ShippingAddress)}</p>
                <p>If you can't make it, reject the delivery in the app so the admin can reassign it.</p>
                <p><a href="{baseUrl}/Driver/Deliveries/Details/{order.Id}">Open delivery</a></p>
                """));
        return null;
    }

    public async Task<string?> MarkReadyForCollectionAsync(int orderId, string baseUrl)
    {
        var order = await LoadAsync(orderId);
        if (order == null) return "Order not found.";
        if (order.IsDelivery) return "This order is for delivery.";
        if (!order.IsPaid || order.IsCompleted) return "Only paid orders that aren't completed can be marked ready.";

        order.Status = OrderStatus.ReadyForCollection;
        NewQr(order);
        AddEvent(order, "Order is ready for collection at the store.");
        await _db.SaveChangesAsync();

        var customer = order.User!;
        await _email.SendAsync(customer.Email!, $"Order #{order.Id} is ready for collection",
            EmailService.Layout(_store.Name, "Ready for collection", $"""
                <p>Hi {Enc(customer.FirstName)},</p>
                <p>Your order <strong>#{order.Id}</strong> is ready to collect at:</p>
                <p style="background:#e8f1ef;border-left:4px solid #183630;padding:12px"><strong>{Enc(_store.Address)}</strong><br>{Enc(_store.CollectionHours)}</p>
                <p>Show the attached QR code at the counter. It is valid until {Date(order.QrExpiresAt!.Value)}.</p>
                <p><a href="{baseUrl}/Orders/Details/{order.Id}">View my order &amp; QR code</a></p>
                """), QrAttachment(order, baseUrl));
        return null;
    }

    public async Task<string?> RegenerateQrAsync(int orderId, string baseUrl)
    {
        var order = await LoadAsync(orderId);
        if (order == null) return "Order not found.";
        if (order.IsCompleted) return "The order is already completed.";
        if (order.Status is not (OrderStatus.DriverAssigned or OrderStatus.OutForDelivery or OrderStatus.ReadyForCollection))
            return order.IsDelivery ? "Assign a driver first." : "Mark the order ready for collection first.";

        NewQr(order);
        AddEvent(order, "A new QR code was issued. The old one no longer works.");
        await _db.SaveChangesAsync();

        var customer = order.User!;
        await _email.SendAsync(customer.Email!, $"New QR code for order #{order.Id}",
            EmailService.Layout(_store.Name, "Your new QR code", $"""
                <p>Hi {Enc(customer.FirstName)},</p>
                <p>A new QR code has been issued for order <strong>#{order.Id}</strong>. Your previous code no longer works.</p>
                <p>It is valid until {Date(order.QrExpiresAt!.Value)}.</p>
                <p><a href="{baseUrl}/Orders/Details/{order.Id}">View my order &amp; QR code</a></p>
                """), QrAttachment(order, baseUrl));
        return null;
    }

    // ---------- Driver actions ----------

    public async Task<string?> RejectAsync(int orderId, string driverId, string reason)
    {
        var order = await LoadAsync(orderId);
        if (order == null || order.DriverId != driverId) return "Delivery not found.";
        if (order.Status != OrderStatus.DriverAssigned) return "You can only reject a delivery before leaving the store.";

        var driverName = order.Driver?.FullName ?? "Driver";
        order.DriverId = null;
        order.DriverAssignedAt = null;
        order.DeliveryWindowEnd = null;
        order.Status = OrderStatus.Paid;
        order.QrToken = null;
        order.QrGeneratedAt = order.QrExpiresAt = order.QrUsedAt = null;
        AddEvent(order, $"{driverName} rejected the delivery: {reason}. Waiting for a new driver.");
        await _db.SaveChangesAsync();

        var adminEmail = _config["AdminAccount:Email"];
        if (!string.IsNullOrEmpty(adminEmail))
            await _email.SendAsync(adminEmail, $"Driver rejected order #{order.Id}",
                EmailService.Layout(_store.Name, "Delivery needs a new driver", $"""
                    <p>{Enc(driverName)} rejected order <strong>#{order.Id}</strong>.</p>
                    <p><strong>Reason:</strong> {Enc(reason)}</p>
                    <p>Please assign another driver in the admin panel.</p>
                    """));
        return null;
    }

    public async Task<string?> DepartAsync(int orderId, string driverId, string baseUrl)
    {
        var order = await LoadAsync(orderId);
        if (order == null || order.DriverId != driverId) return "Delivery not found.";
        if (order.Status != OrderStatus.DriverAssigned) return "This delivery has already started or finished.";

        order.Status = OrderStatus.OutForDelivery;
        order.DepartedAt = DateTime.UtcNow;
        order.DriverLatitude = _store.Latitude;
        order.DriverLongitude = _store.Longitude;
        order.DriverLocationUpdatedAt = DateTime.UtcNow;
        AddEvent(order, $"{order.Driver!.FullName} left the store and is on the way.");
        await _db.SaveChangesAsync();

        var eta = order.EstimatedMinutes.HasValue
            ? DateTime.Now.AddMinutes(order.EstimatedMinutes.Value).ToString("HH:mm")
            : null;
        var customer = order.User!;
        await _email.SendAsync(customer.Email!, $"Your driver is on the way with order #{order.Id}",
            EmailService.Layout(_store.Name, "Your driver is on the way!", $"""
                <p>Hi {Enc(customer.FirstName)},</p>
                <p>{Enc(order.Driver.FullName)} has just left our store with your order <strong>#{order.Id}</strong>.</p>
                {(eta != null ? $"<p style=\"background:#e8f1ef;border-left:4px solid #183630;padding:12px\">Estimated arrival: <strong>around {eta}</strong> (about {order.EstimatedMinutes} minutes, {order.DistanceKm} km).</p>" : "")}
                <p>Please have your QR code ready. The driver will scan it to confirm delivery.</p>
                <p><a href="{baseUrl}/Orders/Track/{order.Id}" style="background:#183630;color:#fff;padding:10px 18px;border-radius:6px;text-decoration:none">Track your driver live</a></p>
                """));
        return null;
    }

    /// <summary>
    /// Confirms a delivery (driver) or a collection (admin) by QR token.
    /// Returns the order on success, or an error message.
    /// </summary>
    public async Task<(Order? order, string? error)> ConfirmByQrAsync(string token, ApplicationUser scanner, bool isAdmin)
    {
        token = token.Trim().ToLowerInvariant();
        var order = await _db.Orders.Include(o => o.User).Include(o => o.Driver)
            .FirstOrDefaultAsync(o => o.QrToken == token);
        if (order == null) return (null, "This QR code isn't valid. It may have been replaced by a newer one.");
        if (order.QrUsedAt.HasValue) return (order, $"This QR code was already used on {order.QrUsedAt.Value.ToLocalTime():dd MMM yyyy HH:mm}.");
        if (order.QrExpiresAt < DateTime.UtcNow) return (order, "This QR code has expired. Ask the store to issue a new one.");

        if (order.IsDelivery)
        {
            if (!isAdmin && order.DriverId != scanner.Id) return (null, "This order is not assigned to you.");
            if (order.Status is not (OrderStatus.OutForDelivery or OrderStatus.DriverAssigned))
                return (order, "This order isn't out for delivery.");
            order.Status = OrderStatus.Delivered;
            AddEvent(order, $"Delivered. QR code scanned by {scanner.FullName}.");
        }
        else
        {
            if (!isAdmin) return (null, "Collection orders are confirmed at the store.");
            if (order.Status != OrderStatus.ReadyForCollection) return (order, "This order isn't ready for collection.");
            order.Status = OrderStatus.Collected;
            AddEvent(order, $"Collected at the store. QR code scanned by {scanner.FullName}.");
        }

        order.QrUsedAt = DateTime.UtcNow;
        order.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var customer = order.User!;
        var verb = order.IsDelivery ? "delivered" : "collected";
        await _email.SendAsync(customer.Email!, $"Order #{order.Id} {verb}",
            EmailService.Layout(_store.Name, $"Order {verb}", $"""
                <p>Hi {Enc(customer.FirstName)},</p>
                <p>Your order <strong>#{order.Id}</strong> was {verb} on {DateTime.Now:dd MMM yyyy 'at' HH:mm}. Enjoy your new clothes!</p>
                """));
        return (order, null);
    }

    public async Task<bool> UpdateDriverLocationAsync(int orderId, string driverId, double lat, double lng)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.DriverId == driverId);
        if (order == null || order.Status != OrderStatus.OutForDelivery) return false;
        order.DriverLatitude = lat;
        order.DriverLongitude = lng;
        order.DriverLocationUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }
}
