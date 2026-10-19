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
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

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

        builder.Entity<ProductImage>()
            .HasOne(i => i.Product).WithMany(p => p.Images).HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<OrderItem>()
            .HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
