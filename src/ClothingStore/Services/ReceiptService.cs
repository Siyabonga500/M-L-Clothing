using System.Globalization;
using System.Net;
using ClothingStore.Data;
using ClothingStore.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ClothingStore.Services;

/// <summary>Proof of payment: a PDF receipt and the email that carries it.</summary>
public class ReceiptService
{
    private const string Brand = "#183630";
    private const string Brand2 = "#023436";
    private const string Tint = "#E8F1EF";

    private readonly ApplicationDbContext _db;
    private readonly EmailService _email;
    private readonly StoreSettings _store;
    private readonly ILogger<ReceiptService> _logger;

    public ReceiptService(ApplicationDbContext db, EmailService email, IOptions<StoreSettings> store, ILogger<ReceiptService> logger)
    {
        _db = db;
        _email = email;
        _store = store.Value;
        _logger = logger;
    }

    public static string FileName(Order order) => $"MandL-receipt-order-{order.Id}.pdf";

    public Task<Order?> LoadAsync(int orderId) =>
        _db.Orders.Include(o => o.Items).Include(o => o.User).FirstOrDefaultAsync(o => o.Id == orderId);

    private static string Local(DateTime? utc) =>
        utc.HasValue ? utc.Value.ToLocalTime().ToString("dd MMM yyyy, HH:mm", CultureInfo.GetCultureInfo("en-ZA")) : "-";

    public byte[] CreatePdf(Order order)
    {
        var customer = order.User;
        return Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(t => t.FontSize(10).FontColor("#1D2B29"));

            page.Header().Column(col =>
            {
                col.Item().Background(Brand).Padding(16).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(_store.Name).FontSize(20).Bold().FontColor(Colors.White);
                        c.Item().Text(_store.Address).FontSize(9).FontColor("#CFE3DF");
                        if (!string.IsNullOrWhiteSpace(_store.Phone))
                            c.Item().Text(_store.Phone).FontSize(9).FontColor("#CFE3DF");
                    });
                    row.ConstantItem(170).AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text("PROOF OF PAYMENT").FontSize(13).Bold().FontColor("#E0A93B");
                        c.Item().AlignRight().Text($"Receipt #{order.Id:D6}").FontSize(10).FontColor(Colors.White);
                    });
                });
            });

            page.Content().PaddingVertical(16).Column(col =>
            {
                col.Spacing(14);

                // Paid stamp + key facts
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Billed to").FontSize(8).Bold().FontColor("#6B7D79");
                        c.Item().Text(order.RecipientName).Bold();
                        if (customer?.Email != null) c.Item().Text(customer.Email);
                        if (!string.IsNullOrWhiteSpace(order.Phone)) c.Item().Text(order.Phone);
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(order.IsDelivery ? "Deliver to" : "Collect at").FontSize(8).Bold().FontColor("#6B7D79");
                        c.Item().Text(order.IsDelivery ? order.ShippingAddress : _store.Address);
                    });
                    row.ConstantItem(120).AlignRight().AlignMiddle()
                        .Border(2).BorderColor(Brand).Padding(8)
                        .AlignCenter().Text("PAID").FontSize(20).Bold().FontColor(Brand);
                });

                col.Item().Background(Tint).Padding(10).Row(row =>
                {
                    void Fact(string label, string value) => row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(label).FontSize(8).Bold().FontColor("#6B7D79");
                        c.Item().Text(value).Bold().FontColor(Brand2);
                    });
                    Fact("Order number", $"#{order.Id}");
                    Fact("Order date", Local(order.CreatedAt));
                    Fact("Payment date", Local(order.PaidAt));
                    Fact("Method", order.IsDelivery ? "Delivery" : "Collection");
                });

                // Items
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(5);
                        c.RelativeColumn(1.4f);
                        c.RelativeColumn(1);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                    });

                    table.Header(h =>
                    {
                        static IContainer Head(IContainer c) => c.Background(Brand).PaddingVertical(6).PaddingHorizontal(6);
                        h.Cell().Element(Head).Text("Item").FontColor(Colors.White).Bold();
                        h.Cell().Element(Head).Text("Size").FontColor(Colors.White).Bold();
                        h.Cell().Element(Head).AlignRight().Text("Qty").FontColor(Colors.White).Bold();
                        h.Cell().Element(Head).AlignRight().Text("Unit price").FontColor(Colors.White).Bold();
                        h.Cell().Element(Head).AlignRight().Text("Amount").FontColor(Colors.White).Bold();
                    });

                    var i = 0;
                    foreach (var item in order.Items)
                    {
                        string bg = i++ % 2 == 0 ? "#FFFFFF" : "#F5F7F6";
                        IContainer Cell(IContainer c) => c.Background(bg).BorderBottom(0.5f).BorderColor("#E1E8E6").PaddingVertical(6).PaddingHorizontal(6);
                        table.Cell().Element(Cell).Text(item.ProductName);
                        table.Cell().Element(Cell).Text(item.Size ?? "-");
                        table.Cell().Element(Cell).AlignRight().Text(item.Quantity.ToString());
                        table.Cell().Element(Cell).AlignRight().Text(item.UnitPrice.ToMoney());
                        table.Cell().Element(Cell).AlignRight().Text(item.LineTotal.ToMoney());
                    }
                });

                // Totals
                col.Item().AlignRight().Width(240).Column(c =>
                {
                    c.Item().Row(r => { r.RelativeItem().Text("Items"); r.RelativeItem().AlignRight().Text(order.Items.Sum(x => x.Quantity).ToString()); });
                    c.Item().Row(r => { r.RelativeItem().Text(order.IsDelivery ? "Delivery" : "Collection"); r.RelativeItem().AlignRight().Text("Free"); });
                    c.Item().PaddingTop(4).BorderTop(1).BorderColor(Brand).PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text("Total paid").Bold().FontSize(12);
                        r.RelativeItem().AlignRight().Text(order.Total.ToMoney()).Bold().FontSize(12).FontColor(Brand);
                    });
                });

                col.Item().Text(t =>
                {
                    t.Span("Paid by card via Stripe. ").FontColor("#6B7D79");
                    if (!string.IsNullOrEmpty(order.StripePaymentIntentId))
                        t.Span($"Payment reference: {order.StripePaymentIntentId}").FontColor("#6B7D79");
                });
            });

            page.Footer().Column(c =>
            {
                c.Item().BorderTop(0.5f).BorderColor("#E1E8E6").PaddingTop(6)
                    .Text($"Thank you for shopping with {_store.Name}. Keep this receipt as proof of payment.")
                    .FontSize(8).FontColor("#6B7D79");
                c.Item().AlignRight().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor("#6B7D79"));
                    t.Span("Page "); t.CurrentPageNumber(); t.Span(" of "); t.TotalPages();
                });
            });
        })).GeneratePdf();
    }

    /// <summary>Emails the receipt (items table in the email, PDF attached) to the customer.</summary>
    public async Task SendAsync(Order order, string? baseUrl)
    {
        var customer = order.User;
        if (customer?.Email == null) return;

        var rows = string.Join("", order.Items.Select(i => $"""
            <tr>
              <td style="padding:8px;border-bottom:1px solid #e1e8e6">{WebUtility.HtmlEncode(i.ProductName)}</td>
              <td style="padding:8px;border-bottom:1px solid #e1e8e6">{WebUtility.HtmlEncode(i.Size ?? "-")}</td>
              <td style="padding:8px;border-bottom:1px solid #e1e8e6;text-align:right">{i.Quantity}</td>
              <td style="padding:8px;border-bottom:1px solid #e1e8e6;text-align:right">{i.LineTotal.ToMoney()}</td>
            </tr>
            """));

        var link = baseUrl == null ? "" :
            $"""<p><a href="{baseUrl}/Orders/Details/{order.Id}" style="background:#183630;color:#fff;padding:10px 18px;border-radius:6px;text-decoration:none">View my order</a></p>""";

        var body = EmailService.Layout(_store.Name, "Payment received – thank you!", $"""
            <p>Hi {WebUtility.HtmlEncode(customer.FirstName)},</p>
            <p>We've received your payment for order <strong>#{order.Id}</strong>. Your proof of payment is attached as a PDF.</p>
            <table style="width:100%;border-collapse:collapse;font-size:14px;margin:12px 0">
              <thead><tr style="background:#183630;color:#fff">
                <th style="padding:8px;text-align:left">Item</th><th style="padding:8px;text-align:left">Size</th>
                <th style="padding:8px;text-align:right">Qty</th><th style="padding:8px;text-align:right">Amount</th>
              </tr></thead>
              <tbody>{rows}</tbody>
              <tfoot><tr>
                <td colspan="3" style="padding:8px;text-align:right;font-weight:bold">Total paid</td>
                <td style="padding:8px;text-align:right;font-weight:bold;color:#183630">{order.Total.ToMoney()}</td>
              </tr></tfoot>
            </table>
            <p><strong>Paid on:</strong> {Local(order.PaidAt)}<br>
               <strong>{(order.IsDelivery ? "Delivery to" : "Collect at")}:</strong> {WebUtility.HtmlEncode(order.IsDelivery ? order.ShippingAddress : _store.Address)}</p>
            {link}
            """);

        var attachments = new List<EmailAttachment>();
        try
        {
            attachments.Add(new EmailAttachment(FileName(order), CreatePdf(order), "application/pdf"));
        }
        catch (Exception ex)
        {
            // Still send the email (with the items listed) if the PDF can't be made.
            _logger.LogError(ex, "Could not create PDF receipt for order {OrderId}", order.Id);
        }

        await _email.SendAsync(customer.Email, $"Proof of payment – order #{order.Id}", body, attachments.ToArray());
    }
}
