namespace OrderService.Core.Models.DTOs;

public class CreateOrderItemDto
{
    public string ProductId { get; set; } = string.Empty; // MongoDB ObjectId as string
    public string ProductName { get; set; } = string.Empty;
    public string? ProductImageUrl { get; set; }
    public string? Sku { get; set; } // Product SKU for inventory tracking
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
