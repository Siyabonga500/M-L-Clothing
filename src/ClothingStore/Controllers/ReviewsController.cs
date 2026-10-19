using ClothingStore.Data;
using ClothingStore.Filters;
using ClothingStore.Models;
using ClothingStore.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Controllers;

/// <summary>
/// Driver ratings. Customers rate the driver of a delivered order (once per order);
/// everyone can read the reviews; logged-in users react with a thumbs up or down.
/// </summary>
public class ReviewsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public ReviewsController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index(string? driverId, string sort = "newest")
    {
        var me = _users.GetUserId(User);
        var drivers = await _users.GetUsersInRoleAsync(Roles.Driver);
        var stats = await _db.DriverReviews.GroupBy(r => r.DriverId)
            .Select(g => new { g.Key, Avg = g.Average(r => r.Rating), Count = g.Count() })
            .ToDictionaryAsync(x => x.Key);
        var delivered = await _db.Orders.Where(o => o.DriverId != null && o.Status == OrderStatus.Delivered)
            .GroupBy(o => o.DriverId!).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var query = _db.DriverReviews.Include(r => r.Customer).Include(r => r.Driver).Include(r => r.Reactions).AsQueryable();
        if (!string.IsNullOrEmpty(driverId)) query = query.Where(r => r.DriverId == driverId);
        var reviews = (await query.ToListAsync()).Select(r => new ReviewItem
        {
            Review = r,
            Likes = r.Reactions.Count(x => x.IsLike),
            Dislikes = r.Reactions.Count(x => !x.IsLike),
            Mine = me == null ? null : r.Reactions.FirstOrDefault(x => x.UserId == me)?.IsLike,
            CanReact = me != null && me != r.DriverId   // drivers can't vote on their own reviews
        });
        reviews = sort switch
        {
            "oldest" => reviews.OrderBy(r => r.Review.CreatedAt),
            "highest" => reviews.OrderByDescending(r => r.Review.Rating).ThenByDescending(r => r.Review.CreatedAt),
            "lowest" => reviews.OrderBy(r => r.Review.Rating).ThenByDescending(r => r.Review.CreatedAt),
            "helpful" => reviews.OrderByDescending(r => r.Likes - r.Dislikes).ThenByDescending(r => r.Review.CreatedAt),
            _ => reviews.OrderByDescending(r => r.Review.CreatedAt)
        };

        return View(new ReviewsPageViewModel
        {
            DriverId = driverId,
            Sort = sort,
            Reviews = reviews.ToList(),
            Drivers = drivers.Select(d => new DriverRatingSummary
            {
                Driver = d,
                Average = stats.TryGetValue(d.Id, out var s) ? s.Avg : 0,
                Count = stats.TryGetValue(d.Id, out var s2) ? s2.Count : 0,
                Delivered = delivered.GetValueOrDefault(d.Id)
            }).OrderByDescending(d => d.Average).ThenByDescending(d => d.Count).ThenBy(d => d.Driver.FirstName).ToList()
        });
    }

    [Authorize, ShoppersOnly, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int orderId, int rating, string? comment)
    {
        var me = _users.GetUserId(User)!;
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == me);
        if (order == null) return NotFound();

        string? error = null;
        if (order.Status != OrderStatus.Delivered || order.DriverId == null) error = "You can rate the driver once your order has been delivered.";
        else if (await _db.DriverReviews.AnyAsync(r => r.OrderId == orderId)) error = "You've already rated the driver for this order.";
        else if (rating is < 1 or > 5) error = "Please choose 1 to 5 stars.";
        else if (comment?.Length > 1000) error = "Please keep your comment under 1000 characters.";
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction("Details", "Orders", new { id = orderId });
        }

        _db.DriverReviews.Add(new DriverReview
        {
            OrderId = orderId,
            DriverId = order.DriverId!,
            CustomerId = me,
            Rating = rating,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim()
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Thank you! Your rating has been posted.";
        return RedirectToAction("Details", "Orders", new { id = orderId });
    }

    /// <summary>Thumbs up / down. Clicking the same one again removes it; clicking the other one switches.</summary>
    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> React(int id, bool like, string? returnUrl)
    {
        var me = _users.GetUserId(User)!;
        var review = await _db.DriverReviews.FindAsync(id);
        if (review == null) return NotFound();

        if (review.DriverId == me)
            TempData["Error"] = "You can't react to reviews about yourself.";
        else
        {
            var existing = await _db.ReviewReactions.FirstOrDefaultAsync(x => x.ReviewId == id && x.UserId == me);
            if (existing == null) _db.ReviewReactions.Add(new ReviewReaction { ReviewId = id, UserId = me, IsLike = like });
            else if (existing.IsLike == like) _db.ReviewReactions.Remove(existing);
            else existing.IsLike = like;
            await _db.SaveChangesAsync();
        }

        var back = Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action(nameof(Index))!;
        return LocalRedirect(back + $"#review-{id}");
    }

    [Authorize(Roles = Roles.Admin), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? returnUrl)
    {
        var review = await _db.DriverReviews.FindAsync(id);
        if (review != null)
        {
            _db.DriverReviews.Remove(review);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Review deleted.";
        }
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action(nameof(Index))!);
    }
}
