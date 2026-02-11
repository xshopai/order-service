namespace OrderService.Core.Models.Events;

/// <summary>
/// Event published when an order is shipped with tracking information
/// </summary>
public class OrderShippedEvent
{
    public Guid OrderId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    
    // Tracking Information
    public string CarrierName { get; set; } = string.Empty;
    public string TrackingNumber { get; set; } = string.Empty;
    public string? TrackingUrl { get; set; }
    public DateTime ShippedDate { get; set; }
    public DateTime? EstimatedDeliveryDate { get; set; }
    
    // Items shipped
    public List<OrderItemEvent> Items { get; set; } = new();
    public AddressEvent ShippingAddress { get; set; } = new();
}
