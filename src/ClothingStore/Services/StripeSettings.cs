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
    public string Name { get; set; } = "Siya Clothing";
    public string CurrencySymbol { get; set; } = "R";
}
