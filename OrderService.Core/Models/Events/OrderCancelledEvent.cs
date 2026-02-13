namespace OrderService.Core.Models.Events;

/// <summary>
/// Event published when an order is cancelled
/// </summary>
public class OrderCancelledEvent
{
    public Guid OrderId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string CancellationReason { get; set; } = string.Empty;
    public string CancelledBy { get; set; } = string.Empty;
    public DateTime CancelledAt { get; set; }
    
    // Financial information for refund processing
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? PaymentTransactionId { get; set; }
    public string? PaymentProvider { get; set; }
    
    // Items to return to inventory
    public List<OrderItemEvent> Items { get; set; } = new();
    
    // Saga compensation tracking
    public bool RequiresPaymentRefund { get; set; }
    public bool RequiresInventoryRelease { get; set; }
}
