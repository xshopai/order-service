using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using OrderService.Core.Data;
using OrderService.Core.Models.Entities;
using OrderService.Core.Models.Enums;

namespace OrderService.Core.Repositories;

/// <summary>
/// Repository implementation for OrderReturn entity operations
/// </summary>
public class OrderReturnRepository : IOrderReturnRepository
{
    private readonly OrderDbContext _context;
    private readonly ILogger<OrderReturnRepository> _logger;

    public OrderReturnRepository(OrderDbContext context, ILogger<OrderReturnRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all returns with their items
    /// </summary>
    public async Task<IEnumerable<OrderReturn>> GetAllReturnsAsync()
    {
        _logger.LogDebug("Fetching all returns from database");
        
        return await _context.OrderReturns
            .Include(r => r.Items)
            .Include(r => r.Order)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get return by ID with items included
    /// </summary>
    public async Task<OrderReturn?> GetReturnByIdAsync(Guid id)
    {
        _logger.LogDebug("Fetching return with ID: {ReturnId}", id);
        
        return await _context.OrderReturns
            .Include(r => r.Items)
            .Include(r => r.Order)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    /// <summary>
    /// Get returns by customer ID
    /// </summary>
    public async Task<IEnumerable<OrderReturn>> GetReturnsByCustomerIdAsync(string customerId)
    {
        _logger.LogDebug("Fetching returns for customer: {CustomerId}", customerId);
        
        return await _context.OrderReturns
            .Include(r => r.Items)
            .Include(r => r.Order)
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get returns by order ID
    /// </summary>
    public async Task<IEnumerable<OrderReturn>> GetReturnsByOrderIdAsync(Guid orderId)
    {
        _logger.LogDebug("Fetching returns for order: {OrderId}", orderId);
        
        return await _context.OrderReturns
            .Include(r => r.Items)
            .Include(r => r.Order)
            .Where(r => r.OrderId == orderId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get returns by status
    /// </summary>
    public async Task<IEnumerable<OrderReturn>> GetReturnsByStatusAsync(ReturnStatus status)
    {
        _logger.LogDebug("Fetching returns with status: {Status}", status);
        
        return await _context.OrderReturns
            .Include(r => r.Items)
            .Include(r => r.Order)
            .Where(r => r.Status == status)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get returns by status with pagination
    /// </summary>
    public async Task<(IEnumerable<OrderReturn> Returns, int TotalCount)> GetReturnsByStatusPagedAsync(
        ReturnStatus? status, 
        int page, 
        int pageSize)
    {
        _logger.LogDebug("Fetching returns with pagination: Status={Status}, Page={Page}, PageSize={PageSize}", 
            status, page, pageSize);
        
        var query = _context.OrderReturns
            .Include(r => r.Items)
            .Include(r => r.Order)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var totalCount = await query.CountAsync();

        var returns = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (returns, totalCount);
    }

    /// <summary>
    /// Create a new return
    /// </summary>
    public async Task<OrderReturn> CreateReturnAsync(OrderReturn orderReturn)
    {
        _logger.LogDebug("Creating new return for order: {OrderId}", orderReturn.OrderId);
        
        _context.OrderReturns.Add(orderReturn);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Created return: {ReturnNumber}", orderReturn.ReturnNumber);
        return orderReturn;
    }

    /// <summary>
    /// Update an existing return
    /// </summary>
    public async Task<OrderReturn> UpdateReturnAsync(OrderReturn orderReturn)
    {
        _logger.LogDebug("Updating return: {ReturnId}", orderReturn.Id);
        
        orderReturn.UpdatedAt = DateTime.UtcNow;
        _context.OrderReturns.Update(orderReturn);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Updated return: {ReturnNumber}", orderReturn.ReturnNumber);
        return orderReturn;
    }

    /// <summary>
    /// Delete a return
    /// </summary>
    public async Task<bool> DeleteReturnAsync(Guid id)
    {
        _logger.LogDebug("Deleting return: {ReturnId}", id);
        
        var orderReturn = await _context.OrderReturns.FindAsync(id);
        if (orderReturn == null)
        {
            _logger.LogWarning("Return not found: {ReturnId}", id);
            return false;
        }

        _context.OrderReturns.Remove(orderReturn);
        await _context.SaveChangesAsync();
        
        _logger.LogInformation("Deleted return: {ReturnNumber}", orderReturn.ReturnNumber);
        return true;
    }

    /// <summary>
    /// Check if return exists
    /// </summary>
    public async Task<bool> ReturnExistsAsync(Guid id)
    {
        return await _context.OrderReturns.AnyAsync(r => r.Id == id);
    }

    /// <summary>
    /// Check if return exists for an order
    /// </summary>
    public async Task<bool> ReturnExistsForOrderAsync(Guid orderId)
    {
        return await _context.OrderReturns.AnyAsync(r => r.OrderId == orderId);
    }

    /// <summary>
    /// Get return statistics
    /// </summary>
    public async Task<Dictionary<string, int>> GetReturnStatisticsAsync()
    {
        _logger.LogDebug("Fetching return statistics");

        var stats = await _context.OrderReturns
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var result = stats.ToDictionary(
            s => s.Status.ToString(),
            s => s.Count
        );

        return result;
    }
}
