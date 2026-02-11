namespace OrderService.Core.Models.Events;

/// <summary>
/// Event published when a return is requested
/// </summary>
public class ReturnRequestedEvent
{
    public Guid ReturnId { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string ReturnNumber { get; set; } = string.Empty;
    public Enums.ReturnReason Reason { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal TotalRefund { get; set; }
    public string Currency { get; set; } = string.Empty;
    public List<ReturnItemEvent> Items { get; set; } = new();
    public DateTime RequestedAt { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

/// <summary>
/// Event published when a return status changes
/// </summary>
public class ReturnStatusChangedEvent
{
    public Guid ReturnId { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public Enums.ReturnStatus Status { get; set; }
    public decimal TotalRefund { get; set; }
    public string Currency { get; set; } = string.Empty;
    public List<ReturnItemEvent> Items { get; set; } = new();
    public DateTime UpdatedAt { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

/// <summary>
/// Return item information for events
/// </summary>
public class ReturnItemEvent
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int QuantityToReturn { get; set; }
    public decimal RefundAmount { get; set; }
}
