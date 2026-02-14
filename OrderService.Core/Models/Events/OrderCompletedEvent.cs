namespace OrderService.Core.Models.Events;

/// <summary>
/// Event published when an order is delivered/completed
/// This triggers inventory deduction for the order items
/// </summary>
public class OrderCompletedEvent
{
    /// <summary>
    /// Order ID - used to find and complete reservations
    /// </summary>
    public Guid OrderId { get; set; }
    
    /// <summary>
    /// Correlation ID for distributed tracing
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;
    
    /// <summary>
    /// Order number for logging/debugging
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Customer ID for audit purposes
    /// </summary>
    public string CustomerId { get; set; } = string.Empty;
    
    /// <summary>
    /// When the order was completed/delivered
    /// </summary>
    public DateTime CompletedAt { get; set; }
    
    /// <summary>
    /// Order items with SKU and quantity for inventory deduction
    /// </summary>
    public List<CompletedOrderItem> Items { get; set; } = new();
}

/// <summary>
/// Order item data for inventory deduction
/// </summary>
public class CompletedOrderItem
{
    /// <summary>
    /// SKU for inventory lookup
    /// </summary>
    public string Sku { get; set; } = string.Empty;
    
    /// <summary>
    /// Product ID (for reference)
    /// </summary>
    public string ProductId { get; set; } = string.Empty;
    
    /// <summary>
    /// Quantity to deduct from inventory
    /// </summary>
    public int Quantity { get; set; }
}
