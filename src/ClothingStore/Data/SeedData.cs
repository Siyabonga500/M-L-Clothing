using ClothingStore.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClothingStore.Data;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
    public const string Driver = "Driver";

    /// <summary>Admins and drivers don't shop, so they get no cart or "My orders".</summary>
    public static bool IsShopper(System.Security.Claims.ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && !user.IsInRole(Admin) && !user.IsInRole(Driver);
}

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { Roles.Admin, Roles.Customer, Roles.Driver })
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

        // "S:10,M:15" -> size rows; Stock is the total.
        static List<ProductSize> Sz(string spec) =>
            spec.Split(',').Select((part, i) =>
            {
                var bits = part.Split(':');
                return new ProductSize { Size = bits[0], Quantity = int.Parse(bits[1]), SortOrder = i };
            }).ToList();

        static List<ProductImage> Pics(params string[] ids) =>
            ids.Select((id, i) => new ProductImage { Url = $"https://images.unsplash.com/{id}?w=800", SortOrder = i }).ToList();

        db.Products.AddRange(
            new Product { Name = "Classic White T-Shirt", Category = "Men", Price = 149.99m, OriginalPrice = 199.99m, Sizes = Sz("S:10,M:15,L:15,XL:10"),
                Description = "Soft 100% cotton crew-neck tee. A wardrobe essential.",
                Images = Pics("photo-1521572163474-6864f9cf17ab", "photo-1622445275463-afa2ab738c34", "photo-1581655353564-df123a1eb820") },
            new Product { Name = "Denim Jacket", Category = "Men", Price = 899.00m, Sizes = Sz("S:4,M:6,L:6,XL:4"),
                Description = "Rugged blue denim jacket with button front and chest pockets.",
                Images = Pics("photo-1576871337622-98d48d1cf531", "photo-1551537482-f2075a1d41f2") },
            new Product { Name = "Slim Fit Chinos", Category = "Men", Price = 449.50m, OriginalPrice = 549.50m, Sizes = Sz("30:8,32:10,34:10,36:7"),
                Description = "Stretch cotton chinos with a modern slim fit.",
                Images = Pics("photo-1473966968600-fa801b869a1a", "photo-1624378439575-d8705ad7ae80") },
            new Product { Name = "Floral Summer Dress", Category = "Women", Price = 599.00m, OriginalPrice = 749.00m, Sizes = Sz("XS:5,S:7,M:7,L:6"),
                Description = "Lightweight floral dress, perfect for warm Durban days.",
                Images = Pics("photo-1572804013309-59a88b7e92f1", "photo-1595777457583-95e059d581b8", "photo-1585487000160-6ebcfceb0d03", "photo-1496747611176-843222e1e57c") },
            new Product { Name = "Knit Sweater", Category = "Women", Price = 649.99m, Sizes = Sz("S:6,M:9,L:9,XL:6"),
                Description = "Cosy oversized knit sweater in a neutral tone.",
                Images = Pics("photo-1434389677669-e08b4cac3105", "photo-1576566588028-4147f3842f27") },
            new Product { Name = "High-Waist Jeans", Category = "Women", Price = 699.00m, Sizes = Sz("26:8,28:12,30:12,32:8"),
                Description = "Flattering high-waist skinny jeans with stretch.",
                Images = Pics("photo-1541099649105-f69ad21f3246", "photo-1582418702059-97ebafb35d09") },
            new Product { Name = "Hooded Sweatshirt", Category = "Unisex", Price = 399.00m, OriginalPrice = 499.00m, Sizes = Sz("S:12,M:18,L:18,XL:12"),
                Description = "Fleece-lined hoodie with kangaroo pocket.",
                Images = Pics("photo-1556821840-3a63f95609a7", "photo-1509942774463-acf339cf87d5") },
            new Product { Name = "Leather Sneakers", Category = "Shoes", Price = 1199.00m, Sizes = Sz("6:2,7:3,8:3,9:3,10:2,11:2"),
                Description = "Minimal white leather sneakers for everyday wear.",
                Images = Pics("photo-1549298916-b41d501d3772", "photo-1600185365483-26d7a4cc7519") }
        );
        foreach (var p in db.Products.Local) p.RecalculateStock();
        await db.SaveChangesAsync();
    }
}
