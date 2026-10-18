using ClothingStore.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Data;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
}

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.Admin, Roles.Customer })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        await EnsureAdminAsync(services, config);
        await SeedProductsAsync(db);
    }

    private static async Task EnsureAdminAsync(IServiceProvider services, IConfiguration config)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var email = config["AdminAccount:Email"] ?? "siya61512@gmail.com";
        var password = config["AdminAccount:Password"] ?? "Siyabonga@2";

        var admin = await userManager.FindByEmailAsync(email);
        if (admin == null)
        {
            admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "Siyabonga",
                Surname = "Admin",
                Gender = "Male",
                HomeAddress = "Head Office"
            };
            var result = await userManager.CreateAsync(admin, password);
            if (!result.Succeeded)
                throw new InvalidOperationException("Could not create admin: " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        if (!await userManager.IsInRoleAsync(admin, Roles.Admin))
            await userManager.AddToRoleAsync(admin, Roles.Admin);
    }

    private static async Task SeedProductsAsync(ApplicationDbContext db)
    {
        if (await db.Products.AnyAsync()) return;

        const string img = "https://images.unsplash.com/";
        db.Products.AddRange(
            new Product { Name = "Classic White T-Shirt", Category = "Men", Price = 199.99m, Stock = 50,
                Description = "Soft 100% cotton crew-neck tee. A wardrobe essential.",
                ImageUrl = img + "photo-1521572163474-6864f9cf17ab?w=600" },
            new Product { Name = "Denim Jacket", Category = "Men", Price = 899.00m, Stock = 20,
                Description = "Rugged blue denim jacket with button front and chest pockets.",
                ImageUrl = img + "photo-1576871337622-98d48d1cf531?w=600" },
            new Product { Name = "Slim Fit Chinos", Category = "Men", Price = 549.50m, Stock = 35, Sizes = "30,32,34,36",
                Description = "Stretch cotton chinos with a modern slim fit.",
                ImageUrl = img + "photo-1473966968600-fa801b869a1a?w=600" },
            new Product { Name = "Floral Summer Dress", Category = "Women", Price = 749.00m, Stock = 25, Sizes = "XS,S,M,L",
                Description = "Lightweight floral dress, perfect for warm days.",
                ImageUrl = img + "photo-1572804013309-59a88b7e92f1?w=600" },
            new Product { Name = "Knit Sweater", Category = "Women", Price = 649.99m, Stock = 30,
                Description = "Cosy oversized knit sweater in a neutral tone.",
                ImageUrl = img + "photo-1434389677669-e08b4cac3105?w=600" },
            new Product { Name = "High-Waist Jeans", Category = "Women", Price = 699.00m, Stock = 40, Sizes = "26,28,30,32",
                Description = "Flattering high-waist skinny jeans with stretch.",
                ImageUrl = img + "photo-1541099649105-f69ad21f3246?w=600" },
            new Product { Name = "Hooded Sweatshirt", Category = "Unisex", Price = 499.00m, Stock = 60,
                Description = "Fleece-lined hoodie with kangaroo pocket.",
                ImageUrl = img + "photo-1556821840-3a63f95609a7?w=600" },
            new Product { Name = "Leather Sneakers", Category = "Shoes", Price = 1199.00m, Stock = 15, Sizes = "6,7,8,9,10,11",
                Description = "Minimal white leather sneakers for everyday wear.",
                ImageUrl = img + "photo-1549298916-b41d501d3772?w=600" }
        );
        await db.SaveChangesAsync();
    }
}
