using OrderService.Core.Models.DTOs;
using OrderService.Core.Models.Enums;

namespace OrderService.Core.Services;

/// <summary>
/// Service interface for order return business logic
/// </summary>
public interface IOrderReturnService
{
    /// <summary>
    /// Get all returns
    /// </summary>
    Task<IEnumerable<ReturnResponseDto>> GetAllReturnsAsync();

    /// <summary>
    /// Get return by ID
    /// </summary>
    Task<ReturnResponseDto?> GetReturnByIdAsync(Guid id);

    /// <summary>
    /// Get returns by customer ID
    /// </summary>
    Task<IEnumerable<ReturnResponseDto>> GetReturnsByCustomerIdAsync(string customerId);

    /// <summary>
    /// Get returns by order ID
    /// </summary>
    Task<IEnumerable<ReturnResponseDto>> GetReturnsByOrderIdAsync(Guid orderId);

    /// <summary>
    /// Get returns by status with pagination
    /// </summary>
    Task<(IEnumerable<ReturnResponseDto> Returns, int TotalCount)> GetReturnsByStatusPagedAsync(
        ReturnStatus? status, 
        int page, 
        int pageSize);

    /// <summary>
    /// Create a new return request
    /// </summary>
    Task<ReturnResponseDto> CreateReturnAsync(CreateReturnRequestDto createReturnDto, string userId, string correlationId = "");

    /// <summary>
    /// Update return status (approve, reject, complete, etc.)
    /// </summary>
    Task<ReturnResponseDto?> UpdateReturnStatusAsync(Guid id, UpdateReturnStatusDto updateStatusDto, string userId, string correlationId = "");

    /// <summary>
    /// Delete a return
    /// </summary>
    Task<bool> DeleteReturnAsync(Guid id);

    /// <summary>
    /// Check if return exists
    /// </summary>
    Task<bool> ReturnExistsAsync(Guid id);

    /// <summary>
    /// Check if order is eligible for return
    /// </summary>
    Task<(bool IsEligible, string? Reason)> IsOrderEligibleForReturnAsync(Guid orderId);

    /// <summary>
    /// Get return statistics
    /// </summary>
    Task<Dictionary<string, int>> GetReturnStatisticsAsync();
}
