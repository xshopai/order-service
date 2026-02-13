namespace OrderService.Core.Models.Events;

/// <summary>
/// Event published when an order is created
/// </summary>
public class OrderCreatedEvent
{
    public Guid OrderId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<OrderItemEvent> Items { get; set; } = new();
    public AddressEvent ShippingAddress { get; set; } = new();
    public AddressEvent BillingAddress { get; set; } = new();
}

/// <summary>
/// Order item event data
/// </summary>
public class OrderItemEvent
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

/// <summary>
/// Address event data
/// </summary>
public class AddressEvent
{
    public string AddressLine1 { get; set; } = string.Empty;
    public string AddressLine2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
