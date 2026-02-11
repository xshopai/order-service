using System.ComponentModel.DataAnnotations;

namespace OrderService.Core.Models.DTOs;

/// <summary>
/// Request DTO for cancelling an order
/// </summary>
public class CancelOrderDto
{
    [Required]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Cancellation reason must be between 5 and 500 characters")]
    public string CancellationReason { get; set; } = string.Empty;
}