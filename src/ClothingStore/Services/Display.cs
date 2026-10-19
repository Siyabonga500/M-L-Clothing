using ClothingStore.Models;

namespace ClothingStore.Services;

public static class DisplayExtensions
{
    public static string Display(this OrderStatus status) => status switch
    {
        OrderStatus.PendingPayment => "Awaiting payment",
        OrderStatus.Paid => "Paid",
        OrderStatus.Processing => "Processing",
        OrderStatus.DriverAssigned => "Driver assigned",
        OrderStatus.OutForDelivery => "Out for delivery",
        OrderStatus.Delivered => "Delivered",
        OrderStatus.ReadyForCollection => "Ready for collection",
        OrderStatus.Collected => "Collected",
        OrderStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };
}
