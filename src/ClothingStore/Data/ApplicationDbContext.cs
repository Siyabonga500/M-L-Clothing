using ClothingStore.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderEvent> OrderEvents => Set<OrderEvent>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductSize> ProductSizes => Set<ProductSize>();
    public DbSet<ProductColor> ProductColors => Set<ProductColor>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
    public DbSet<DriverReview> DriverReviews => Set<DriverReview>();
    public DbSet<ReviewReaction> ReviewReactions => Set<ReviewReaction>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<CartItem>()
            .HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<CartItem>()
            .HasOne(c => c.Product).WithMany().HasForeignKey(c => c.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Order>()
            .HasOne(o => o.User).WithMany().HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Order>()
            .HasOne(o => o.Driver).WithMany().HasForeignKey(o => o.DriverId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Order>().HasIndex(o => o.StripeSessionId);
        builder.Entity<Order>().HasIndex(o => o.QrToken).IsUnique();

        // Reviews: one per order. Deleting a customer or driver removes their reviews;
        // reactions go with their review or user.
        builder.Entity<DriverReview>().HasIndex(r => r.OrderId).IsUnique();
        builder.Entity<DriverReview>()
            .HasOne(r => r.Order).WithMany().HasForeignKey(r => r.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<DriverReview>()
            .HasOne(r => r.Driver).WithMany().HasForeignKey(r => r.DriverId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<DriverReview>()
            .HasOne(r => r.Customer).WithMany().HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.NoAction);
        builder.Entity<ReviewReaction>().HasIndex(x => new { x.ReviewId, x.UserId }).IsUnique();
        builder.Entity<ReviewReaction>()
            .HasOne(x => x.Review).WithMany(r => r.Reactions).HasForeignKey(x => x.ReviewId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<ReviewReaction>()
            .HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProductSize>()
            .HasOne(s => s.Product).WithMany(p => p.Sizes).HasForeignKey(s => s.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<ProductSize>().HasIndex(s => new { s.ProductId, s.Size }).IsUnique();

        builder.Entity<ProductColor>()
            .HasOne(c => c.Product).WithMany(p => p.Colors).HasForeignKey(c => c.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<ProductColor>().HasIndex(c => new { c.ProductId, c.Name }).IsUnique();

        builder.Entity<ProductImage>()
            .HasOne(i => i.Product).WithMany(p => p.Images).HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<OrderItem>()
            .HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
