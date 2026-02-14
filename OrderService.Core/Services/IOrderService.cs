using OrderService.Core.Models.DTOs;
using OrderService.Core.Models.Enums;

namespace OrderService.Core.Services;

/// <summary>
/// Service interface for order business logic
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// Get all orders
    /// </summary>
    Task<IEnumerable<OrderResponseDto>> GetAllOrdersAsync();

    /// <summary>
    /// Get order by ID
    /// </summary>
    Task<OrderResponseDto?> GetOrderByIdAsync(Guid id);

    /// <summary>
    /// Get orders by customer ID
    /// </summary>
    Task<IEnumerable<OrderResponseDto>> GetOrdersByCustomerIdAsync(string customerId);

    /// <summary>
    /// Create a new order
    /// </summary>
    Task<OrderResponseDto> CreateOrderAsync(CreateOrderDto createOrderDto, string correlationId = "");

    /// <summary>
    /// Update order status
    /// </summary>
    Task<OrderResponseDto?> UpdateOrderStatusAsync(Guid id, UpdateOrderStatusDto updateStatusDto);

    /// <summary>
    /// Get orders by status
    /// </summary>
    Task<IEnumerable<OrderResponseDto>> GetOrdersByStatusAsync(OrderStatus status);

    /// <summary>
    /// Delete an order
    /// </summary>
    Task<bool> DeleteOrderAsync(Guid id);

    /// <summary>
    /// Get orders with pagination and filtering
    /// </summary>
    Task<PagedResponseDto<OrderResponseDto>> GetOrdersPagedAsync(OrderQueryDto query);

    /// <summary>
    /// Get orders by customer ID with pagination
    /// </summary>
    Task<PagedResponseDto<OrderResponseDto>> GetOrdersByCustomerIdPagedAsync(string customerId, PagedRequestDto pageRequest);

    /// <summary>
    /// Get order statistics for admin dashboard
    /// </summary>
    Task<OrderStatsDto> GetStatsAsync(bool includeRecent = false, int recentLimit = 10);

    /// <summary>
    /// Cancel an order
    /// </summary>
    Task<OrderResponseDto?> CancelOrderAsync(Guid id, CancelOrderDto cancelOrderDto, string correlationId = "");

    /// <summary>
    /// Update order tracking information
    /// </summary>
    Task<OrderResponseDto?> UpdateTrackingAsync(Guid id, UpdateTrackingDto updateTrackingDto, string correlationId = "");

    /// <summary>
    /// Get order tracking information
    /// </summary>
    Task<TrackingInfoDto?> GetTrackingInfoAsync(Guid id);

    /// <summary>
    /// Validate if a user made a purchase of a product
    /// Used by review-service for verified purchase validation
    /// </summary>
    Task<ValidatePurchaseResponseDto> ValidatePurchaseAsync(ValidatePurchaseRequestDto request, string correlationId = "");
}
