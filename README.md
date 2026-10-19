# Siya Clothing – ASP.NET Core MVC store with Stripe

An online clothing shop built with **ASP.NET Core MVC (.NET 10)**, **ASP.NET Identity**, **Entity Framework Core (SQLite)** and **Stripe Checkout**.

## Features

**Customers**
- Browse clothing on the home page (search + category filter) without logging in.
- Clicking **Add to cart** while logged out sends you to the **Login** page; after logging in the item is added automatically.
- Register with Name, Surname, Email, Gender, Date of birth, Phone, Home address, City, Province, Postal code, Password and Confirm password.
- Cart: change quantity/size, remove items. Stock is checked.
- Checkout: confirm delivery details, then pay on Stripe's secure hosted page.
- Payment is verified with Stripe before the order is marked **Paid**; stock is reduced and the cart is emptied.
- **My Orders**: view order history and details. Edit your profile.

**Admin** (`/Admin`)
- Default login: **siya61512@gmail.com** / **Siyabonga@2** (created automatically on first run).
- Dashboard: revenue, order, user and product counts, recent orders.
- Users: search, view, edit, lock/unlock, make/remove admin, reset password, delete.
- Products: create, edit, hide, delete (products with orders are hidden instead of deleted).
- Orders: filter by status, view details, update status (Paid → Processing → Shipped → Delivered / Cancelled).

## Run it in Visual Studio 2026

1. Clone the repo: **File → Clone Repository…**, paste the GitHub URL, and choose branch `claude/beautiful-heisenberg-paysqt`.
   (Or clone it yourself and open `ClothingStore.sln` with **File → Open → Project/Solution**.)
2. Visual Studio restores the NuGet packages automatically. Wait for it to finish (bottom status bar).
3. Pick the **https** profile in the green Run button drop-down and press **F5** (or **Ctrl+F5** to run without debugging).
4. The first time, Visual Studio asks you to trust the ASP.NET Core development certificate. Click **Yes** both times.
5. The browser opens on the shop. The SQLite database (`clothingstore.db`) is created in the project folder and filled with sample products and the admin account on first start. Nothing else to install: no SQL Server needed.

To start from a clean database, stop the app and delete `src/ClothingStore/clothingstore.db`.

### From the command line

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download) (included with Visual Studio 2026).

```bash
cd src/ClothingStore
dotnet run
```

## Stripe setup

1. Create a free account at https://dashboard.stripe.com and switch to **Test mode**.
2. Copy your keys from **Developers → API keys** into `src/ClothingStore/appsettings.json`:

   ```json
   "Stripe": {
     "PublishableKey": "pk_test_...",
     "SecretKey": "sk_test_...",
     "WebhookSecret": "",
     "Currency": "zar"
   }
   ```

   Better: keep the secret out of source control with user secrets. In Visual Studio, right-click the
   **ClothingStore** project → **Manage User Secrets**, and paste:

   ```json
   { "Stripe:SecretKey": "sk_test_..." }
   ```

   Or from the command line:

   ```bash
   cd src/ClothingStore
   dotnet user-secrets init
   dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
   ```

3. Pay with the test card **4242 4242 4242 4242**, any future expiry date, any CVC.

**Optional webhook** (needs the [Stripe CLI](https://docs.stripe.com/stripe-cli)) marks orders paid even if the customer closes the browser before returning. The app runs on https://localhost:7139 by default:

```bash
stripe listen --forward-to https://localhost:7139/Checkout/Webhook
```

Put the `whsec_...` value it prints into `Stripe:WebhookSecret`.

## Configuration

| Setting | Purpose |
| --- | --- |
| `ConnectionStrings:DefaultConnection` | SQLite file path |
| `Store:Name`, `Store:CurrencySymbol` | Shop name and price symbol shown on pages |
| `Stripe:Currency` | Currency sent to Stripe (default `zar`) |
| `AdminAccount:Email`, `AdminAccount:Password` | Admin account created on first run |

Change the admin password after first login in production (Admin → Users → reset password), and move secrets out of `appsettings.json`.

## Project layout

```
src/ClothingStore
├── Areas/Admin          Admin dashboard, users, products, orders
├── Controllers          Home, Products, Account, Cart, Checkout (Stripe), Orders
├── Data                 DbContext + seed data (roles, admin, sample products)
├── Migrations           EF Core migrations
├── Models               ApplicationUser, Product, CartItem, Order, OrderItem
├── Services             CartService, OrderService, Stripe settings
├── ViewModels           Form and page models
└── Views                Razor views (Bootstrap 5)
```
