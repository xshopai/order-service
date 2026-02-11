using System.ComponentModel.DataAnnotations;
using OrderService.Core.Models.Enums;

namespace OrderService.Core.Models.Entities;

public class OrderReturn
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid OrderId { get; set; }
    
    [Required]
    [StringLength(24)]
    public string CustomerId { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string ReturnNumber { get; set; } = string.Empty;
    
    public ReturnStatus Status { get; set; } = ReturnStatus.Requested;
    public ReturnReason Reason { get; set; }
    
    [Required]
    [StringLength(1000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;
    
    // Items being returned
    public List<ReturnItem> Items { get; set; } = new();
    
    // Refund information
    public decimal RefundAmount { get; set; }
    public decimal ShippingRefund { get; set; }
    public decimal TotalRefund { get; set; }
    
    [StringLength(3)]
    public string Currency { get; set; } = "USD";
    
    // Return shipping
    public string? ReturnShippingCarrier { get; set; }
    public string? ReturnTrackingNumber { get; set; }
    public DateTime? ItemsReceivedDate { get; set; }
    
    // Processing details
    public string? RejectionReason { get; set; }
    public string? InspectionNotes { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime? RefundProcessedDate { get; set; }
    
    // Audit trail
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? ApprovedBy { get; set; }
    public string? ProcessedBy { get; set; }
    
    // Navigation property
    public Order? Order { get; set; }
}

public class ReturnItem
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid OrderReturnId { get; set; }
    public Guid OrderItemId { get; set; }
    
    [Required]
    [StringLength(50)]
    public string ProductId { get; set; } = string.Empty;
    
    [Required]
    [StringLength(200)]
    public string ProductName { get; set; } = string.Empty;
    
    public int QuantityToReturn { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal RefundAmount { get; set; }
    
    [StringLength(200)]
    public string? ProductImageUrl { get; set; }
    
    [StringLength(500)]
    public string? ItemCondition { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
