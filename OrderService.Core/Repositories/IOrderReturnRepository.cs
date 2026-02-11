using OrderService.Core.Models.Entities;
using OrderService.Core.Models.Enums;

namespace OrderService.Core.Repositories;

/// <summary>
/// Repository interface for OrderReturn entity operations
/// </summary>
public interface IOrderReturnRepository
{
    /// <summary>
    /// Get all returns with their items
    /// </summary>
    Task<IEnumerable<OrderReturn>> GetAllReturnsAsync();

    /// <summary>
    /// Get return by ID with items included
    /// </summary>
    Task<OrderReturn?> GetReturnByIdAsync(Guid id);

    /// <summary>
    /// Get returns by customer ID
    /// </summary>
    Task<IEnumerable<OrderReturn>> GetReturnsByCustomerIdAsync(string customerId);

    /// <summary>
    /// Get returns by order ID
    /// </summary>
    Task<IEnumerable<OrderReturn>> GetReturnsByOrderIdAsync(Guid orderId);

    /// <summary>
    /// Get returns by status
    /// </summary>
    Task<IEnumerable<OrderReturn>> GetReturnsByStatusAsync(ReturnStatus status);

    /// <summary>
    /// Get returns by status with pagination
    /// </summary>
    Task<(IEnumerable<OrderReturn> Returns, int TotalCount)> GetReturnsByStatusPagedAsync(
        ReturnStatus? status, 
        int page, 
        int pageSize);

    /// <summary>
    /// Create a new return
    /// </summary>
    Task<OrderReturn> CreateReturnAsync(OrderReturn orderReturn);

    /// <summary>
    /// Update an existing return
    /// </summary>
    Task<OrderReturn> UpdateReturnAsync(OrderReturn orderReturn);

    /// <summary>
    /// Delete a return
    /// </summary>
    Task<bool> DeleteReturnAsync(Guid id);

    /// <summary>
    /// Check if return exists
    /// </summary>
    Task<bool> ReturnExistsAsync(Guid id);

    /// <summary>
    /// Check if return exists for an order
    /// </summary>
    Task<bool> ReturnExistsForOrderAsync(Guid orderId);

    /// <summary>
    /// Get return statistics
    /// </summary>
    Task<Dictionary<string, int>> GetReturnStatisticsAsync();
}
