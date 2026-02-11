using System.ComponentModel.DataAnnotations;

namespace OrderService.Core.Models.DTOs;

/// <summary>
/// Request DTO for updating order tracking information
/// </summary>
public class UpdateTrackingDto
{
    [Required]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Carrier name is required")]
    public string CarrierName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Tracking number is required")]
    public string TrackingNumber { get; set; } = string.Empty;

    [StringLength(200)]
    public string? TrackingUrl { get; set; }

    public DateTime? EstimatedDeliveryDate { get; set; }
}

/// <summary>
/// Response DTO for tracking information
/// </summary>
public class TrackingInfoDto
{
    public string? CarrierName { get; set; }
    public string? TrackingNumber { get; set; }
    public string? TrackingUrl { get; set; }
    public DateTime? ShippedDate { get; set; }
    public DateTime? EstimatedDeliveryDate { get; set; }
    public DateTime? DeliveredDate { get; set; }
    public string ShippingStatus { get; set; } = string.Empty;
    public List<TrackingEventDto> Timeline { get; set; } = new();
}

/// <summary>
/// Order status timeline event
/// </summary>
public class TrackingEventDto
{
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Location { get; set; }
    public bool IsCompleted { get; set; }
}
