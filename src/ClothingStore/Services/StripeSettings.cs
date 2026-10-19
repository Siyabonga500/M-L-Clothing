namespace ClothingStore.Services;

public class StripeSettings
{
    public string PublishableKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string Currency { get; set; } = "zar";
}

public class StoreSettings
{
    public string Name { get; set; } = "M & L Clothing";
    public string CurrencySymbol { get; set; } = "R";
    public string Address { get; set; } = "277 Monty Naicker Rd, Durban Central, Durban, 4001";
    public double Latitude { get; set; } = -29.8583;
    public double Longitude { get; set; } = 31.0247;
    public string Phone { get; set; } = "";

    /// <summary>Shown on the Contact page. Contact form messages go here (or to the admin if empty).</summary>
    public string Email { get; set; } = "";

    // Big photo banner at the top of the shop page.
    public string HeroTitle { get; set; } = "Fashion Redefined";
    public string HeroSubtitle { get; set; } = "Quality clothing for every occasion";
    public string HeroImageUrl { get; set; } = "/img/hero-shop.svg";
    public string CollectionHours { get; set; } = "Mon–Fri 08:00–17:00, Sat 08:00–13:00";

    /// <summary>Customers are told their order arrives within this many days of driver assignment.</summary>
    public int DeliveryDays { get; set; } = 3;

    /// <summary>How long a delivery/collection QR code stays valid.</summary>
    public int QrValidHours { get; set; } = 120;

    /// <summary>Average city driving speed used for delivery time estimates.</summary>
    public double AverageSpeedKmh { get; set; } = 35;

    /// <summary>Delivery fee for distances up to <see cref="DeliveryBaseKm"/>.</summary>
    public decimal DeliveryBaseFee { get; set; } = 90m;

    public double DeliveryBaseKm { get; set; } = 10;

    /// <summary>Charged for every kilometre (or part of one) beyond <see cref="DeliveryBaseKm"/>.</summary>
    public decimal DeliveryPerKmFee { get; set; } = 10m;

    /// <summary>R90 up to 10 km; after that R10 for each extra km (a started km counts as a full km).</summary>
    public decimal DeliveryFee(double km)
    {
        if (km <= DeliveryBaseKm) return DeliveryBaseFee;
        var extraKm = (int)Math.Ceiling(Math.Round(km - DeliveryBaseKm, 1));
        return DeliveryBaseFee + extraKm * DeliveryPerKmFee;
    }
}

public class EmailSettings
{
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "M & L Clothing";
}
