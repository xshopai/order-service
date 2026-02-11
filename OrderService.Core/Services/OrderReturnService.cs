using OrderService.Core.Messaging;
using OrderService.Core.Models.DTOs;
using OrderService.Core.Models.Entities;
using OrderService.Core.Models.Enums;
using OrderService.Core.Models.Events;
using OrderService.Core.Repositories;
using OrderService.Core.Utils;

namespace OrderService.Core.Services;

/// <summary>
/// Service implementation for order return business logic
/// </summary>
public class OrderReturnService : IOrderReturnService
{
    // Business constants
    private const string RETURN_NUMBER_PREFIX = "RET";
    private const int RETURN_ELIGIBILITY_DAYS = 30; // 30 days from delivery
    private const string DEFAULT_CURRENCY = "USD";

    private readonly IOrderReturnRepository _returnRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly StandardLogger _logger;
    private readonly IMessagingProvider _messagingProvider;

    public OrderReturnService(
        IOrderReturnRepository returnRepository,
        IOrderRepository orderRepository,
        StandardLogger logger,
        IMessagingProvider messagingProvider)
    {
        _returnRepository = returnRepository;
        _orderRepository = orderRepository;
        _logger = logger;
        _messagingProvider = messagingProvider;
    }

    /// <summary>
    /// Get all returns
    /// </summary>
    public async Task<IEnumerable<ReturnResponseDto>> GetAllReturnsAsync()
    {
        _logger.Info("Getting all returns", null, new { operation = "GET_ALL_RETURNS" });
        
        try
        {
            var returns = await _returnRepository.GetAllReturnsAsync();
            var returnDtos = returns.Select(MapToReturnResponseDto).ToList();
            
            _logger.Info("Retrieved all returns", null, new { returnCount = returnDtos.Count });
            
            return returnDtos;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to get all returns", ex);
            throw;
        }
    }

    /// <summary>
    /// Get return by ID
    /// </summary>
    public async Task<ReturnResponseDto?> GetReturnByIdAsync(Guid id)
    {
        _logger.Info($"Getting return by ID: {id}", null, new { operation = "GET_RETURN_BY_ID", returnId = id });
        
        try
        {
            var orderReturn = await _returnRepository.GetReturnByIdAsync(id);
            if (orderReturn == null)
            {
                _logger.Warn($"Return with ID {id} not found", null, new { returnId = id });
                return null;
            }

            _logger.Info($"Retrieved return {orderReturn.ReturnNumber} (ID: {id})", null, 
                new { returnId = id, returnNumber = orderReturn.ReturnNumber });
            
            return MapToReturnResponseDto(orderReturn);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to get return {id}", ex, null, new { returnId = id });
            throw;
        }
    }

    /// <summary>
    /// Get returns by customer ID
    /// </summary>
    public async Task<IEnumerable<ReturnResponseDto>> GetReturnsByCustomerIdAsync(string customerId)
    {
        _logger.Info($"Getting returns for customer: {customerId}", null, 
            new { operation = "GET_RETURNS_BY_CUSTOMER", customerId });
        
        try
        {
            var returns = await _returnRepository.GetReturnsByCustomerIdAsync(customerId);
            var returnDtos = returns.Select(MapToReturnResponseDto).ToList();
            
            _logger.Info($"Retrieved {returnDtos.Count} returns for customer {customerId}", null, 
                new { customerId, returnCount = returnDtos.Count });
            
            return returnDtos;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to get returns for customer {customerId}", ex, null, new { customerId });
            throw;
        }
    }

    /// <summary>
    /// Get returns by order ID
    /// </summary>
    public async Task<IEnumerable<ReturnResponseDto>> GetReturnsByOrderIdAsync(Guid orderId)
    {
        _logger.Info($"Getting returns for order: {orderId}", null, 
            new { operation = "GET_RETURNS_BY_ORDER", orderId });
        
        try
        {
            var returns = await _returnRepository.GetReturnsByOrderIdAsync(orderId);
            var returnDtos = returns.Select(MapToReturnResponseDto).ToList();
            
            _logger.Info($"Retrieved {returnDtos.Count} returns for order {orderId}", null, 
                new { orderId, returnCount = returnDtos.Count });
            
            return returnDtos;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to get returns for order {orderId}", ex, null, new { orderId });
            throw;
        }
    }

    /// <summary>
    /// Get returns by status with pagination
    /// </summary>
    public async Task<(IEnumerable<ReturnResponseDto> Returns, int TotalCount)> GetReturnsByStatusPagedAsync(
        ReturnStatus? status, 
        int page, 
        int pageSize)
    {
        _logger.Info("Getting returns with pagination", null, new { 
            operation = "GET_RETURNS_PAGED", 
            status = status?.ToString() ?? "All", 
            page, 
            pageSize 
        });
        
        try
        {
            var (returns, totalCount) = await _returnRepository.GetReturnsByStatusPagedAsync(status, page, pageSize);
            var returnDtos = returns.Select(MapToReturnResponseDto).ToList();
            
            _logger.Info($"Retrieved {returnDtos.Count} returns (Total: {totalCount})", null, 
                new { returnCount = returnDtos.Count, totalCount, page, pageSize });
            
            return (returnDtos, totalCount);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to get returns with pagination", ex, null, new { status, page, pageSize });
            throw;
        }
    }

    /// <summary>
    /// Create a new return request
    /// </summary>
    public async Task<ReturnResponseDto> CreateReturnAsync(CreateReturnRequestDto createReturnDto, string userId, string correlationId = "")
    {
        var currentCorrelationId = !string.IsNullOrEmpty(correlationId) ? correlationId : Guid.NewGuid().ToString();
        
        _logger.Info("Creating return request", currentCorrelationId, new {
            orderId = createReturnDto.OrderId,
            reason = createReturnDto.Reason.ToString(),
            itemCount = createReturnDto.Items?.Count ?? 0,
            userId
        });

        try
        {
            // Validate order exists and get details
            var order = await _orderRepository.GetOrderByIdAsync(createReturnDto.OrderId);
            if (order == null)
            {
                _logger.Error($"Order not found: {createReturnDto.OrderId}", null, currentCorrelationId);
                throw new InvalidOperationException($"Order with ID {createReturnDto.OrderId} not found");
            }

            // Check if order is eligible for return
            var (isEligible, reason) = await IsOrderEligibleForReturnAsync(createReturnDto.OrderId);
            if (!isEligible)
            {
                _logger.Warn($"Order not eligible for return: {reason}", currentCorrelationId, 
                    new { orderId = createReturnDto.OrderId, reason });
                throw new InvalidOperationException($"Order is not eligible for return: {reason}");
            }

            // Validate return items
            ValidateReturnItems(createReturnDto.Items, order);

            // Calculate refund amounts
            var (refundAmount, shippingRefund, totalRefund) = CalculateRefundAmounts(createReturnDto.Items, order);

            // Generate return number
            var returnNumber = GenerateReturnNumber();

            // Create return entity
            var orderReturn = new OrderReturn
            {
                OrderId = createReturnDto.OrderId,
                CustomerId = order.CustomerId,
                ReturnNumber = returnNumber,
                Status = ReturnStatus.Requested,
                Reason = createReturnDto.Reason,
                Description = createReturnDto.Description,
                RefundAmount = refundAmount,
                ShippingRefund = shippingRefund,
                TotalRefund = totalRefund,
                Currency = order.Currency,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Add return items
            orderReturn.Items = createReturnDto.Items.Select(itemDto =>
            {
                var orderItem = order.Items.First(oi => oi.Id == itemDto.OrderItemId);
                return new ReturnItem
                {
                    OrderItemId = itemDto.OrderItemId,
                    ProductId = orderItem.ProductId,
                    ProductName = orderItem.ProductName,
                    QuantityToReturn = itemDto.QuantityToReturn,
                    UnitPrice = orderItem.UnitPrice,
                    RefundAmount = orderItem.UnitPrice * itemDto.QuantityToReturn,
                    ProductImageUrl = orderItem.ProductImageUrl,
                    ItemCondition = itemDto.ItemCondition,
                    CreatedAt = DateTime.UtcNow
                };
            }).ToList();

            // Save to database
            var createdReturn = await _returnRepository.CreateReturnAsync(orderReturn);

            _logger.Info($"Created return request: {returnNumber}", currentCorrelationId, new {
                returnId = createdReturn.Id,
                returnNumber,
                orderId = createReturnDto.OrderId,
                customerId = order.CustomerId,
                totalRefund
            });

            // Publish return.requested event
            await PublishReturnRequestedEventAsync(createdReturn, order, currentCorrelationId);

            return MapToReturnResponseDto(createdReturn);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to create return request", ex, currentCorrelationId, new { 
                orderId = createReturnDto.OrderId 
            });
            throw;
        }
    }

    /// <summary>
    /// Update return status
    /// </summary>
    public async Task<ReturnResponseDto?> UpdateReturnStatusAsync(Guid id, UpdateReturnStatusDto updateStatusDto, string userId, string correlationId = "")
    {
        var currentCorrelationId = !string.IsNullOrEmpty(correlationId) ? correlationId : Guid.NewGuid().ToString();
        
        _logger.Info($"Updating return status: {id}", currentCorrelationId, new {
            returnId = id,
            newStatus = updateStatusDto.Status.ToString(),
            userId
        });

        try
        {
            var orderReturn = await _returnRepository.GetReturnByIdAsync(id);
            if (orderReturn == null)
            {
                _logger.Warn($"Return not found: {id}", currentCorrelationId, new { returnId = id });
                return null;
            }

            var oldStatus = orderReturn.Status;
            var newStatus = updateStatusDto.Status;

            // Validate status transition
            ValidateStatusTransition(oldStatus, newStatus);

            // Update status and related fields
            orderReturn.Status = newStatus;
            orderReturn.UpdatedAt = DateTime.UtcNow;

            switch (newStatus)
            {
                case ReturnStatus.Approved:
                    orderReturn.ApprovedDate = DateTime.UtcNow;
                    orderReturn.ApprovedBy = userId;
                    if (!string.IsNullOrEmpty(updateStatusDto.Notes))
                    {
                        orderReturn.InspectionNotes = updateStatusDto.Notes;
                    }
                    break;

                case ReturnStatus.Rejected:
                    orderReturn.RejectionReason = updateStatusDto.RejectionReason ?? updateStatusDto.Notes ?? "Not specified";
                    break;

                case ReturnStatus.ItemsReceived:
                    orderReturn.ItemsReceivedDate = DateTime.UtcNow;
                    break;

                case ReturnStatus.Inspecting:
                    if (!string.IsNullOrEmpty(updateStatusDto.Notes))
                    {
                        orderReturn.InspectionNotes = updateStatusDto.Notes;
                    }
                    break;

                case ReturnStatus.Completed:
                    orderReturn.CompletedDate = DateTime.UtcNow;
                    orderReturn.ProcessedBy = userId;
                    break;

                case ReturnStatus.RefundProcessed:
                    orderReturn.RefundProcessedDate = DateTime.UtcNow;
                    orderReturn.ProcessedBy = userId;
                    break;
            }

            // Save changes
            var updatedReturn = await _returnRepository.UpdateReturnAsync(orderReturn);

            _logger.Info($"Updated return status from {oldStatus} to {newStatus}", currentCorrelationId, new {
                returnId = id,
                returnNumber = orderReturn.ReturnNumber,
                oldStatus = oldStatus.ToString(),
                newStatus = newStatus.ToString()
            });

            // Publish appropriate event based on status
            await PublishReturnStatusEventAsync(updatedReturn, newStatus, currentCorrelationId);

            return MapToReturnResponseDto(updatedReturn);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to update return status: {id}", ex, currentCorrelationId, new { returnId = id });
            throw;
        }
    }

    /// <summary>
    /// Delete a return
    /// </summary>
    public async Task<bool> DeleteReturnAsync(Guid id)
    {
        _logger.Info($"Deleting return: {id}", null, new { operation = "DELETE_RETURN", returnId = id });
        
        try
        {
            var result = await _returnRepository.DeleteReturnAsync(id);
            
            if (result)
            {
                _logger.Info($"Deleted return: {id}", null, new { returnId = id });
            }
            else
            {
                _logger.Warn($"Return not found for deletion: {id}", null, new { returnId = id });
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to delete return: {id}", ex, null, new { returnId = id });
            throw;
        }
    }

    /// <summary>
    /// Check if return exists
    /// </summary>
    public async Task<bool> ReturnExistsAsync(Guid id)
    {
        return await _returnRepository.ReturnExistsAsync(id);
    }

    /// <summary>
    /// Check if order is eligible for return
    /// </summary>
    public async Task<(bool IsEligible, string? Reason)> IsOrderEligibleForReturnAsync(Guid orderId)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId);
        
        if (order == null)
        {
            return (false, "Order not found");
        }

        // Check if order is delivered
        if (order.Status != OrderStatus.Delivered)
        {
            return (false, "Order must be delivered before requesting a return");
        }

        // Check if return window has passed (30 days from delivery)
        if (order.DeliveredDate.HasValue)
        {
            var daysSinceDelivery = (DateTime.UtcNow - order.DeliveredDate.Value).Days;
            if (daysSinceDelivery > RETURN_ELIGIBILITY_DAYS)
            {
                return (false, $"Return window has closed (must be within {RETURN_ELIGIBILITY_DAYS} days of delivery)");
            }
        }

        // Check if return already exists for this order
        var existingReturn = await _returnRepository.ReturnExistsForOrderAsync(orderId);
        if (existingReturn)
        {
            return (false, "A return request already exists for this order");
        }

        return (true, null);
    }

    /// <summary>
    /// Get return statistics
    /// </summary>
    public async Task<Dictionary<string, int>> GetReturnStatisticsAsync()
    {
        _logger.Info("Getting return statistics", null, new { operation = "GET_RETURN_STATS" });
        
        try
        {
            var stats = await _returnRepository.GetReturnStatisticsAsync();
            
            _logger.Info("Retrieved return statistics", null, new { statCount = stats.Count });
            
            return stats;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to get return statistics", ex);
            throw;
        }
    }

    // Private helper methods

    private void ValidateReturnItems(List<ReturnItemDto> returnItems, Order order)
    {
        if (returnItems == null || !returnItems.Any())
        {
            throw new InvalidOperationException("Return request must include at least one item");
        }

        foreach (var returnItem in returnItems)
        {
            var orderItem = order.Items.FirstOrDefault(oi => oi.Id == returnItem.OrderItemId);
            if (orderItem == null)
            {
                throw new InvalidOperationException($"Order item {returnItem.OrderItemId} not found in order");
            }

            if (returnItem.QuantityToReturn <= 0 || returnItem.QuantityToReturn > orderItem.Quantity)
            {
                throw new InvalidOperationException($"Invalid return quantity for item {orderItem.ProductName}. Must be between 1 and {orderItem.Quantity}");
            }
        }
    }

    private (decimal refundAmount, decimal shippingRefund, decimal totalRefund) CalculateRefundAmounts(
        List<ReturnItemDto> returnItems, 
        Order order)
    {
        decimal refundAmount = 0;
        decimal shippingRefund = 0;

        foreach (var returnItem in returnItems)
        {
            var orderItem = order.Items.First(oi => oi.Id == returnItem.OrderItemId);
            refundAmount += orderItem.UnitPrice * returnItem.QuantityToReturn;
        }

        // Include shipping refund if all items are being returned
        var totalOrderedQuantity = order.Items.Sum(i => i.Quantity);
        var totalReturnQuantity = returnItems.Sum(i => i.QuantityToReturn);
        
        if (totalReturnQuantity == totalOrderedQuantity)
        {
            shippingRefund = order.ShippingCost;
        }

        decimal totalRefund = refundAmount + shippingRefund;

        return (refundAmount, shippingRefund, totalRefund);
    }

    private void ValidateStatusTransition(ReturnStatus currentStatus, ReturnStatus newStatus)
    {
        // Define valid transitions
        var validTransitions = new Dictionary<ReturnStatus, List<ReturnStatus>>
        {
            { ReturnStatus.Requested, new List<ReturnStatus> { ReturnStatus.Approved, ReturnStatus.Rejected } },
            { ReturnStatus.Approved, new List<ReturnStatus> { ReturnStatus.ItemsReceived } },
            { ReturnStatus.ItemsReceived, new List<ReturnStatus> { ReturnStatus.Inspecting } },
            { ReturnStatus.Inspecting, new List<ReturnStatus> { ReturnStatus.Completed } },
            { ReturnStatus.Completed, new List<ReturnStatus> { ReturnStatus.RefundProcessed } },
            { ReturnStatus.Rejected, new List<ReturnStatus>() }, // Terminal state
            { ReturnStatus.RefundProcessed, new List<ReturnStatus>() } // Terminal state
        };

        if (!validTransitions[currentStatus].Contains(newStatus))
        {
            throw new InvalidOperationException(
                $"Invalid status transition from {currentStatus} to {newStatus}");
        }
    }

    private string GenerateReturnNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var random = new Random().Next(1000, 9999);
        return $"{RETURN_NUMBER_PREFIX}-{timestamp}-{random}";
    }

    private async Task PublishReturnRequestedEventAsync(OrderReturn orderReturn, Order order, string correlationId)
    {
        try
        {
            var returnEvent = new ReturnRequestedEvent
            {
                ReturnId = orderReturn.Id,
                OrderId = orderReturn.OrderId,
                OrderNumber = order.OrderNumber,
                CustomerId = orderReturn.CustomerId,
                CustomerEmail = order.CustomerEmail ?? string.Empty,
                CustomerName = order.CustomerName ?? string.Empty,
                ReturnNumber = orderReturn.ReturnNumber,
                Reason = orderReturn.Reason,
                Description = orderReturn.Description,
                TotalRefund = orderReturn.TotalRefund,
                Currency = orderReturn.Currency,
                Items = orderReturn.Items.Select(i => new ReturnItemEvent
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    QuantityToReturn = i.QuantityToReturn,
                    RefundAmount = i.RefundAmount
                }).ToList(),
                RequestedAt = orderReturn.CreatedAt,
                CorrelationId = correlationId
            };

            await _messagingProvider.PublishEventAsync("return.requested", returnEvent, correlationId);

            _logger.Info("Published return.requested event", correlationId, new {
                returnId = orderReturn.Id,
                returnNumber = orderReturn.ReturnNumber,
                eventType = "return.requested"
            });
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to publish return.requested event", ex, correlationId, new {
                returnId = orderReturn.Id
            });
            // Don't throw - event publishing failure shouldn't fail the operation
        }
    }

    private async Task PublishReturnStatusEventAsync(OrderReturn orderReturn, ReturnStatus newStatus, string correlationId)
    {
        try
        {
            string eventType = newStatus switch
            {
                ReturnStatus.Approved => "return.approved",
                ReturnStatus.Rejected => "return.rejected",
                ReturnStatus.Completed => "return.completed",
                ReturnStatus.RefundProcessed => "return.refund_processed",
                _ => null
            };

            if (eventType == null)
            {
                return; // No event to publish for this status
            }

            var returnEvent = new ReturnStatusChangedEvent
            {
                ReturnId = orderReturn.Id,
                ReturnNumber = orderReturn.ReturnNumber,
                OrderId = orderReturn.OrderId,
                CustomerId = orderReturn.CustomerId,
                Status = newStatus,
                TotalRefund = orderReturn.TotalRefund,
                Currency = orderReturn.Currency,
                Items = orderReturn.Items.Select(i => new ReturnItemEvent
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    QuantityToReturn = i.QuantityToReturn,
                    RefundAmount = i.RefundAmount
                }).ToList(),
                UpdatedAt = orderReturn.UpdatedAt,
                CorrelationId = correlationId
            };

            await _messagingProvider.PublishEventAsync(eventType, returnEvent, correlationId);

            _logger.Info($"Published {eventType} event", correlationId, new {
                returnId = orderReturn.Id,
                returnNumber = orderReturn.ReturnNumber,
                eventType
            });
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to publish return status event", ex, correlationId, new {
                returnId = orderReturn.Id,
                status = newStatus.ToString()
            });
            // Don't throw - event publishing failure shouldn't fail the operation
        }
    }

    private ReturnResponseDto MapToReturnResponseDto(OrderReturn orderReturn)
    {
        return new ReturnResponseDto
        {
            Id = orderReturn.Id,
            OrderId = orderReturn.OrderId,
            OrderNumber = orderReturn.Order?.OrderNumber ?? string.Empty,
            CustomerId = orderReturn.CustomerId,
            ReturnNumber = orderReturn.ReturnNumber,
            Status = orderReturn.Status.ToString(),
            Reason = orderReturn.Reason.ToString(),
            Description = orderReturn.Description,
            Items = orderReturn.Items.Select(i => new ReturnItemResponseDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                QuantityToReturn = i.QuantityToReturn,
                UnitPrice = i.UnitPrice,
                RefundAmount = i.RefundAmount,
                ProductImageUrl = i.ProductImageUrl,
                ItemCondition = i.ItemCondition
            }).ToList(),
            RefundAmount = orderReturn.RefundAmount,
            ShippingRefund = orderReturn.ShippingRefund,
            TotalRefund = orderReturn.TotalRefund,
            Currency = orderReturn.Currency,
            ReturnShippingCarrier = orderReturn.ReturnShippingCarrier,
            ReturnTrackingNumber = orderReturn.ReturnTrackingNumber,
            ItemsReceivedDate = orderReturn.ItemsReceivedDate,
            RejectionReason = orderReturn.RejectionReason,
            InspectionNotes = orderReturn.InspectionNotes,
            ApprovedDate = orderReturn.ApprovedDate,
            ApprovedBy = orderReturn.ApprovedBy,
            CompletedDate = orderReturn.CompletedDate,
            RefundProcessedDate = orderReturn.RefundProcessedDate,
            ProcessedBy = orderReturn.ProcessedBy,
            CreatedAt = orderReturn.CreatedAt,
            UpdatedAt = orderReturn.UpdatedAt
        };
    }
}
