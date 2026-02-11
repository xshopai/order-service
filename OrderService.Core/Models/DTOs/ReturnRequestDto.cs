using System.ComponentModel.DataAnnotations;
using OrderService.Core.Models.Enums;

namespace OrderService.Core.Models.DTOs;

/// <summary>
/// Request DTO for creating a return request
/// </summary>
public class CreateReturnRequestDto
{
    [Required]
    public Guid OrderId { get; set; }

    [Required]
    public ReturnReason Reason { get; set; }

    [Required]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 1000 characters")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MinLength(1, ErrorMessage = "At least one item must be selected for return")]
    public List<ReturnItemDto> Items { get; set; } = new();
}

/// <summary>
/// Item in a return request
/// </summary>
public class ReturnItemDto
{
    [Required]
    public Guid OrderItemId { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
    public int QuantityToReturn { get; set; }

    [StringLength(500)]
    public string? ItemCondition { get; set; }
}

/// <summary>
/// Response DTO for return requests
/// </summary>
public class ReturnResponseDto
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string ReturnNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ReturnItemResponseDto> Items { get; set; } = new();
    public decimal RefundAmount { get; set; }
    public decimal ShippingRefund { get; set; }
    public decimal TotalRefund { get; set; }
    public string Currency { get; set; } = "USD";
    public string? ReturnShippingCarrier { get; set; }
    public string? ReturnTrackingNumber { get; set; }
    public DateTime? ItemsReceivedDate { get; set; }
    public string? RejectionReason { get; set; }
    public string? InspectionNotes { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime? RefundProcessedDate { get; set; }
    public string? ProcessedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Return item response
/// </summary>
public class ReturnItemResponseDto
{
    public Guid Id { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int QuantityToReturn { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal RefundAmount { get; set; }
    public string? ProductImageUrl { get; set; }
    public string? ItemCondition { get; set; }
}

/// <summary>
/// DTO for updating return status (admin only)
/// </summary>
public class UpdateReturnStatusDto
{
    [Required]
    public ReturnStatus Status { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [StringLength(500)]
    public string? RejectionReason { get; set; }
}
