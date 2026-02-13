namespace OrderService.Core.Models.Events;

/// <summary>
/// Event published when an order status changes
/// Used for order updates, cancellations, shipping, and delivery notifications
/// </summary>
public class OrderStatusChangedEvent
{
    /// <summary>
    /// Order unique identifier
    /// </summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// Order number (user-friendly identifier)
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Customer unique identifier
    /// </summary>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Customer email address for notifications
    /// </summary>
    public string CustomerEmail { get; set; } = string.Empty;

    /// <summary>
    /// Previous order status
    /// </summary>
    public string PreviousStatus { get; set; } = string.Empty;

    /// <summary>
    /// New order status
    /// </summary>
    public string NewStatus { get; set; } = string.Empty;

    /// <summary>
    /// When the status was changed
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Who updated the status (user ID or system)
    /// </summary>
    public string UpdatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Optional reason or notes for the status change
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Correlation ID for tracing
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;
}
