using System.Globalization;

namespace ClothingStore.Services;

public static class Money
{
    public static string Symbol { get; set; } = "R";

    public static string ToMoney(this decimal amount) =>
        $"{Symbol} {amount.ToString("N2", CultureInfo.InvariantCulture)}";
}
