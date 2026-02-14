namespace OrderService.Core.Models.DTOs;

/// <summary>
/// Request DTO for validating if a user made a purchase of a product
/// Used by review-service to verify purchase before allowing verified reviews
/// </summary>
public class ValidatePurchaseRequestDto
{
    /// <summary>
    /// The user ID to validate purchase for
    /// </summary>
    public required string UserId { get; set; }
    
    /// <summary>
    /// The product ID to check if purchased
    /// </summary>
    public required string ProductId { get; set; }
    
    /// <summary>
    /// Optional order reference to validate against specific order
    /// </summary>
    public string? OrderReference { get; set; }
}

/// <summary>
/// Response DTO for purchase validation
/// </summary>
public class ValidatePurchaseResponseDto
{
    /// <summary>
    /// Whether the purchase is valid (user purchased the product)
    /// </summary>
    public bool IsValid { get; set; }
    
    /// <summary>
    /// The order ID if purchase was found
    /// </summary>
    public Guid? OrderId { get; set; }
    
    /// <summary>
    /// The order number if purchase was found
    /// </summary>
    public string? OrderNumber { get; set; }
    
    /// <summary>
    /// When the purchase was made
    /// </summary>
    public DateTime? PurchaseDate { get; set; }
    
    /// <summary>
    /// Message explaining validation result
    /// </summary>
    public string? Message { get; set; }
}
