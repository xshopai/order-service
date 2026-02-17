using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using OrderService.Core.Messaging;
using OrderService.Core.Models.DTOs;
using OrderService.Core.Models.Enums;
using OrderService.Core.Models.Events;
using OrderService.Core.Services;
using OrderService.Core.Utils;

namespace OrderService.Controllers;

/// <summary>
/// Admin-specific endpoints for order management
/// </summary>
[ApiController]
[Route("api/admin/orders")]
[Authorize(Policy = "AdminOnly")]
public class AdminOrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly StandardLogger _logger;
    private readonly IMessagingProvider _messagingProvider;

    public AdminOrdersController(
        IOrderService orderService,
        StandardLogger logger,
        IMessagingProvider messagingProvider)
    {
        _orderService = orderService;
        _logger = logger;
        _messagingProvider = messagingProvider;
    }

    /// <summary>
    /// Get all orders (Admin only)
    /// </summary>
    /// <route>GET /api/admin/orders</route>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetOrders()
    {
        var correlationId = GetCorrelationId();

        try
        {
            var orders = await _orderService.GetAllOrdersAsync();

            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.Error("Error fetching all orders", ex, correlationId);
            return StatusCode(500, "An error occurred while fetching orders");
        }
    }

    /// <summary>
    /// Get orders with pagination and filtering (Admin only)
    /// </summary>
    /// <route>GET /api/admin/orders/paged</route>
    [HttpGet("paged")]
    public async Task<ActionResult<PagedResponseDto<OrderResponseDto>>> GetOrdersPaged([FromQuery] OrderQueryDto query)
    {
        var correlationId = GetCorrelationId();

        try
        {
            var pagedOrders = await _orderService.GetOrdersPagedAsync(query);

            return Ok(pagedOrders);
        }
        catch (Exception ex)
        {
            _logger.Error("Error fetching paged orders", ex, correlationId);
            return StatusCode(500, "An error occurred while fetching orders");
        }
    }

    /// <summary>
    /// Get a single order by ID (Admin only)
    /// </summary>
    /// <route>GET /api/admin/orders/{id}</route>
    [HttpGet("{id}")]
    public async Task<ActionResult<OrderResponseDto>> GetOrderById(Guid id)
    {
        var correlationId = GetCorrelationId();
        _logger.Info("Getting order by ID", correlationId, new
        {
            endpoint = "GET /api/admin/orders/{id}",
            orderId = id
        });

        try
        {
            var order = await _orderService.GetOrderByIdAsync(id);

            if (order == null)
            {
                _logger.Warn($"Order with ID {id} not found", correlationId, new {
                    orderId = id,
                    endpoint = "GET /api/admin/orders/{id}"
                });
                return NotFound($"Order with ID {id} not found");
            }

            _logger.Info("Retrieved order by ID", correlationId, new
            {
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                status = order.Status,
                totalAmount = order.TotalAmount
            });

            return Ok(order);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error fetching order {id}", ex, correlationId);
            return StatusCode(500, "An error occurred while fetching the order");
        }
    }

    /// <summary>
    /// Get order statistics for admin dashboard
    /// </summary>
    /// <route>GET /api/admin/orders/stats</route>
    [HttpGet("stats")]
    public async Task<ActionResult<OrderStatsDto>> GetStats(
        [FromQuery] bool includeRecent = false,
        [FromQuery] int recentLimit = 10)
    {
        var correlationId = GetCorrelationId();
        _logger.Info("Getting order statistics", correlationId, new
        {
            endpoint = "GET /api/admin/orders/stats",
            includeRecent,
            recentLimit
        });

        try
        {
            var stats = await _orderService.GetStatsAsync(includeRecent, recentLimit);

            _logger.Info("Retrieved order statistics", correlationId, new
            {
                total = stats.Total,
                pending = stats.Pending,
                completed = stats.Completed,
                revenue = stats.Revenue,
                endpoint = "GET /api/admin/orders/stats"
            });

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to get order statistics", ex, correlationId, new
            {
                endpoint = "GET /api/admin/orders/stats",
                includeRecent,
                recentLimit
            });
            return StatusCode(500, "An error occurred while fetching order statistics");
        }
    }

    /// <summary>
    /// Get orders by status (Admin only)
    /// </summary>
    /// <route>GET /api/admin/orders/status/{status}</route>
    [HttpGet("status/{status}")]
    public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetOrdersByStatus(OrderStatus status)
    {
        var correlationId = GetCorrelationId();
        
        try
        {
            var orders = await _orderService.GetOrdersByStatusAsync(status);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error fetching orders with status: {status}", ex, correlationId);
            return StatusCode(500, "An error occurred while fetching orders");
        }
    }

    /// <summary>
    /// Update order status (Admin only)
    /// </summary>
    /// <route>PUT /api/admin/orders/{id}/status</route>
    [HttpPut("{id}/status")]
    public async Task<ActionResult<OrderResponseDto>> UpdateOrderStatus(Guid id, UpdateOrderStatusDto updateStatusDto)
    {
        var correlationId = GetCorrelationId();
        
        try
        {
            var order = await _orderService.UpdateOrderStatusAsync(id, updateStatusDto);
            
            if (order == null)
            {
                _logger.Warn($"Order with ID {id} not found", correlationId, new {
                    orderId = id,
                    endpoint = "PUT /api/admin/orders/{id}/status"
                });
                return NotFound($"Order with ID {id} not found");
            }

            // Publish order status changed event via Dapr
            try
            {
                var orderEvent = new OrderStatusChangedEvent
                {
                    OrderId = order.Id.ToString(),
                    OrderNumber = order.OrderNumber,
                    CustomerId = order.CustomerId,
                    CustomerEmail = order.CustomerEmail,
                    PreviousStatus = updateStatusDto.Status.ToString(),
                    NewStatus = updateStatusDto.Status.ToString(),
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = GetCurrentUserId() ?? "system",
                    CorrelationId = correlationId
                };

                string topicName = updateStatusDto.Status switch
                {
                    OrderStatus.Cancelled => "order.cancelled",
                    OrderStatus.Shipped => "order.shipped",
                    OrderStatus.Delivered => "order.delivered",
                    _ => "order.status.changed"
                };

                await _messagingProvider.PublishEventAsync(topicName, orderEvent, correlationId);

                _logger.Info($"Published order status changed event: {topicName}", correlationId, new {
                    orderId = order.Id,
                    orderNumber = order.OrderNumber,
                    status = updateStatusDto.Status,
                    topic = topicName
                });
            }
            catch (Exception eventEx)
            {
                _logger.Warn($"Failed to publish order status changed event, but order was updated: {eventEx.Message}", correlationId, new {
                    orderId = order.Id,
                    status = updateStatusDto.Status,
                    error = eventEx.Message
                });
            }

            _logger.Info("ORDER_STATUS_UPDATED", correlationId, new {
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                status = updateStatusDto.Status
            });

            return Ok(order);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error updating order status for order {id}", ex, correlationId);
            return StatusCode(500, "An error occurred while updating the order status");
        }
    }

    /// <summary>
    /// Delete an order (Admin only)
    /// </summary>
    /// <route>DELETE /api/admin/orders/{id}</route>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteOrder(Guid id)
    {
        var correlationId = GetCorrelationId();

        try
        {
            // Get order details before deletion for event publishing
            var order = await _orderService.GetOrderByIdAsync(id);
            
            if (order == null)
            {
                _logger.Warn($"Order with ID {id} not found", correlationId, new {
                    orderId = id,
                    endpoint = "DELETE /api/admin/orders/{id}"
                });
                return NotFound($"Order with ID {id} not found");
            }

            var result = await _orderService.DeleteOrderAsync(id);
            
            if (!result)
            {
                _logger.Warn($"Failed to delete order with ID {id}", correlationId, new {
                    orderId = id,
                    endpoint = "DELETE /api/admin/orders/{id}"
                });
                return NotFound($"Order with ID {id} not found");
            }

            // Publish order deleted event via Dapr
            try
            {
                var orderDeletedEvent = new OrderDeletedEvent
                {
                    OrderId = order.Id.ToString(),
                    OrderNumber = order.OrderNumber,
                    CustomerId = order.CustomerId,
                    DeletedAt = DateTime.UtcNow,
                    DeletedBy = GetCurrentUserId() ?? "system",
                    CorrelationId = correlationId
                };

                await _messagingProvider.PublishEventAsync("order-deleted", orderDeletedEvent, correlationId);

                _logger.Info("Published order deleted event", correlationId, new {
                    orderId = order.Id,
                    orderNumber = order.OrderNumber
                });
            }
            catch (Exception eventEx)
            {
                _logger.Warn($"Failed to publish order deleted event, but order was deleted: {eventEx.Message}", correlationId, new {
                    orderId = order.Id,
                    error = eventEx.Message
                });
            }

            _logger.Info("ORDER_DELETED", correlationId, new {
                orderId = order.Id,
                orderNumber = order.OrderNumber
            });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.Error($"Error deleting order {id}", ex, correlationId);
            return StatusCode(500, "An error occurred while deleting the order");
        }
    }

    /// <summary>
    /// Get order tracking information with timeline (Admin only)
    /// </summary>
    /// <route>GET /api/admin/orders/{id}/tracking</route>
    [HttpGet("{id}/tracking")]
    public async Task<ActionResult<TrackingInfoDto>> GetTracking(Guid id)
    {
        var correlationId = GetCorrelationId();

        try
        {
            var trackingInfo = await _orderService.GetTrackingInfoAsync(id);

            if (trackingInfo == null)
            {
                _logger.Warn($"Order with ID {id} not found", correlationId, new {
                    orderId = id,
                    endpoint = "GET /api/admin/orders/{id}/tracking"
                });
                return NotFound($"Order with ID {id} not found");
            }

            _logger.Info("Retrieved order tracking info", correlationId, new {
                orderId = id,
                shippingStatus = trackingInfo.ShippingStatus,
                timelineEvents = trackingInfo.Timeline.Count
            });

            return Ok(trackingInfo);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error fetching tracking info for order {id}", ex, correlationId);
            return StatusCode(500, "An error occurred while fetching tracking information");
        }
    }

    /// <summary>
    /// Update order tracking information (Admin only)
    /// </summary>
    /// <route>PUT /api/admin/orders/{id}/tracking</route>
    [HttpPut("{id}/tracking")]
    public async Task<ActionResult<OrderResponseDto>> UpdateTracking(Guid id, [FromBody] UpdateTrackingDto updateTrackingDto)
    {
        var correlationId = GetCorrelationId();

        try
        {
            var order = await _orderService.UpdateTrackingAsync(id, updateTrackingDto, correlationId);

            if (order == null)
            {
                _logger.Warn($"Order with ID {id} not found", correlationId, new {
                    orderId = id,
                    endpoint = "PUT /api/admin/orders/{id}/tracking"
                });
                return NotFound($"Order with ID {id} not found");
            }

            _logger.Info("ORDER_TRACKING_UPDATED", correlationId, new {
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                carrier = updateTrackingDto.CarrierName,
                trackingNumber = updateTrackingDto.TrackingNumber
            });

            return Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            _logger.Warn($"Invalid tracking update: {ex.Message}", correlationId, new {
                orderId = id,
                error = ex.Message
            });
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.Error($"Error updating tracking for order {id}", ex, correlationId);
            return StatusCode(500, "An error occurred while updating tracking information");
        }
    }

    /// <summary>
    /// Helper method to get correlation ID from context
    /// </summary>
    private string GetCorrelationId()
    {
        return HttpContext.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();
    }
    
    /// <summary>
    /// Helper method to get current user ID from JWT token
    /// </summary>
    private string? GetCurrentUserId()
    {
        // Try 'id' claim first (from auth-service), then fall back to 'sub' (standard)
        return User.FindFirst("id")?.Value ?? User.FindFirst("sub")?.Value;
    }
}
