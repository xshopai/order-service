using Microsoft.Extensions.Options;
using OrderService.Core.Configuration;
using OrderService.Core.Messaging;
using OrderService.Core.Models.DTOs;
using OrderService.Core.Models.Entities;
using OrderService.Core.Models.Enums;
using OrderService.Core.Models.Events;
using OrderService.Core.Repositories;
using OrderService.Core.Utils;

namespace OrderService.Core.Services;

/// <summary>
/// Service implementation for order business logic with enhanced logging and tracing
/// </summary>
public class OrderService : IOrderService
{
    // Business constants - TODO: Move to database/Admin UI in future
    private const string DEFAULT_CURRENCY = "USD";
    private const decimal TAX_RATE = 0.08m;
    private const decimal FREE_SHIPPING_THRESHOLD = 100.0m;
    private const decimal DEFAULT_SHIPPING_COST = 10.0m;
    private const string ORDER_NUMBER_PREFIX = "ORD";

    private readonly IOrderRepository _orderRepository;
    private readonly StandardLogger _logger;
    private readonly IMessagingProvider _messagingProvider;
    private readonly ICurrentUserService _currentUserService;

    public OrderService(
        IOrderRepository orderRepository, 
        StandardLogger logger,
        IMessagingProvider messagingProvider,
        ICurrentUserService currentUserService)
    {
        _orderRepository = orderRepository;
        _logger = logger;
        _messagingProvider = messagingProvider;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get all orders
    /// </summary>
    public async Task<IEnumerable<OrderResponseDto>> GetAllOrdersAsync()
    {
        _logger.Info("Getting all orders", null, new { operation = "GET_ALL_ORDERS" });
        
        try
        {
            var orders = await _orderRepository.GetAllOrdersAsync();
            var orderDtos = orders.Select(MapToOrderResponseDto).ToList();
            
            _logger.Info("Retrieved all orders", null, new { orderCount = orderDtos.Count });
            
            return orderDtos;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to get all orders", ex);
            throw;
        }
    }

    /// <summary>
    /// Get order by ID
    /// </summary>
    public async Task<OrderResponseDto?> GetOrderByIdAsync(Guid id)
    {
        _logger.Info($"Getting order by ID: {id}", null, new { operation = "GET_ORDER_BY_ID", orderId = id });
        
        try
        {
            var order = await _orderRepository.GetOrderByIdAsync(id);
            if (order == null)
            {
                _logger.Warn($"Order with ID {id} not found", null, new { orderId = id });
                return null;
            }

            _logger.Info($"Retrieved order {order.OrderNumber} (ID: {id})", null, new { orderId = id, orderNumber = order.OrderNumber });
            
            return MapToOrderResponseDto(order);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to get order {id}", ex, null, new { orderId = id });
            throw;
        }
    }

    /// <summary>
    /// Get orders by customer ID
    /// </summary>
    public async Task<IEnumerable<OrderResponseDto>> GetOrdersByCustomerIdAsync(string customerId)
    {
        _logger.Info($"Getting orders for customer: {customerId}", null, new { operation = "GET_ORDERS_BY_CUSTOMER", customerId });
        
        try
        {
            var orders = await _orderRepository.GetOrdersByCustomerIdAsync(customerId);
            var orderDtos = orders.Select(MapToOrderResponseDto).ToList();
            
            _logger.Info($"Retrieved {orderDtos.Count} orders for customer {customerId}", null, new { customerId, orderCount = orderDtos.Count });
            
            return orderDtos;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to get orders for customer {customerId}", ex, null, new { customerId });
            throw;
        }
    }

    /// <summary>
    /// Create a new order
    /// </summary>
    public async Task<OrderResponseDto> CreateOrderAsync(CreateOrderDto createOrderDto, string correlationId = "")
    {
        // Use provided correlation ID or generate new one
        var currentCorrelationId = !string.IsNullOrEmpty(correlationId) ? correlationId : Guid.NewGuid().ToString();
        
        // Get current user info
        var currentUser = _currentUserService.GetUserName() ?? _currentUserService.GetUserId() ?? "System";
        
        _logger.Info("Creating order", currentCorrelationId, new {
            customerId = createOrderDto.CustomerId,
            orderItemsCount = createOrderDto.Items?.Count ?? 0,
            createdBy = currentUser
        });

        try
        {
            // Generate order number using configuration
            var orderNumber = GenerateOrderNumber();

            // Create order entity
            var order = new Order
            {
                CustomerId = createOrderDto.CustomerId,
                CustomerName = createOrderDto.CustomerName,
                CustomerEmail = createOrderDto.CustomerEmail,
                CustomerPhone = createOrderDto.CustomerPhone,
                OrderNumber = orderNumber,
                Status = OrderStatus.Created,
                PaymentStatus = PaymentStatus.Pending,
                ShippingStatus = ShippingStatus.NotShipped,
                Currency = DEFAULT_CURRENCY,
                CreatedBy = currentUser,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Add order items and calculate totals
            decimal subtotal = 0;
            if (createOrderDto.Items != null)
            {
                foreach (var itemDto in createOrderDto.Items)
            {
                var orderItem = new OrderItem
                {
                    ProductId = itemDto.ProductId,
                    ProductName = itemDto.ProductName,
                    ProductImageUrl = itemDto.ProductImageUrl,
                    UnitPrice = itemDto.UnitPrice,
                    Quantity = itemDto.Quantity,
                    TotalPrice = itemDto.UnitPrice * itemDto.Quantity,
                    DiscountAmount = 0,
                    TaxAmount = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                order.Items.Add(orderItem);
                subtotal += orderItem.TotalPrice;
            }
            }

            // Set addresses
            order.ShippingAddress = new Address
            {
                AddressLine1 = createOrderDto.ShippingAddress.AddressLine1,
                AddressLine2 = createOrderDto.ShippingAddress.AddressLine2,
                City = createOrderDto.ShippingAddress.City,
                State = createOrderDto.ShippingAddress.State,
                ZipCode = createOrderDto.ShippingAddress.ZipCode,
                Country = createOrderDto.ShippingAddress.Country
            };

            order.BillingAddress = new Address
            {
                AddressLine1 = createOrderDto.BillingAddress.AddressLine1,
                AddressLine2 = createOrderDto.BillingAddress.AddressLine2,
                City = createOrderDto.BillingAddress.City,
                State = createOrderDto.BillingAddress.State,
                ZipCode = createOrderDto.BillingAddress.ZipCode,
                Country = createOrderDto.BillingAddress.Country
            };

            // Calculate order totals
            order.Subtotal = subtotal;
            order.TaxAmount = subtotal * TAX_RATE;
            order.ShippingCost = subtotal > FREE_SHIPPING_THRESHOLD ? 0 : DEFAULT_SHIPPING_COST;
            order.DiscountAmount = 0;
            order.TotalAmount = order.Subtotal + order.TaxAmount + order.ShippingCost - order.DiscountAmount;

            // Save to database
            var createdOrder = await _orderRepository.CreateOrderAsync(order);
            
            _logger.Info("Order created", currentCorrelationId, new {
                orderId = createdOrder.Id,
                orderNumber = createdOrder.OrderNumber,
                totalAmount = createdOrder.TotalAmount,
                itemCount = createdOrder.Items.Count
            });

            _logger.Business("ORDER_CREATED", currentCorrelationId, new {
                orderId = createdOrder.Id,
                orderNumber = createdOrder.OrderNumber,
                customerId = createdOrder.CustomerId,
                totalAmount = createdOrder.TotalAmount
            });

            // Publish order created event for loose coupling
            try
            {
                var orderCreatedEvent = MapToOrderCreatedEvent(createdOrder, currentCorrelationId);
                await _messagingProvider.PublishEventAsync(
                    "order.created", 
                    orderCreatedEvent,
                    currentCorrelationId);
                
                _logger.Info("Published OrderCreated event", currentCorrelationId, new {
                    orderNumber = createdOrder.OrderNumber,
                    eventType = "ORDER_CREATED_EVENT_PUBLISHED"
                });
            }
            catch (Exception eventEx)
            {
                // Log error but don't fail the order creation
                _logger.Error("Failed to publish OrderCreated event", eventEx, currentCorrelationId, new {
                    orderNumber = createdOrder.OrderNumber,
                    error = eventEx.Message
                });
            }

            return MapToOrderResponseDto(createdOrder);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to create order", ex, currentCorrelationId, new {
                customerId = createOrderDto.CustomerId
            });
            throw;
        }
    }

    /// <summary>
    /// Update order status
    /// </summary>
    public async Task<OrderResponseDto?> UpdateOrderStatusAsync(Guid id, UpdateOrderStatusDto updateStatusDto)
    {
        var currentUser = _currentUserService.GetUserName() ?? _currentUserService.GetUserId() ?? "System";
        _logger.Info("Updating order status", null, new {
            orderId = id,
            newStatus = updateStatusDto.Status,
            updatedBy = currentUser
        });

        try
        {
            var order = await _orderRepository.GetOrderByIdAsync(id);
            if (order == null)
            {
                _logger.Warn($"Order with ID {id} not found", null, new { orderId = id });
                return null;
            }

            var oldStatus = order.Status;
            order.Status = updateStatusDto.Status;
            order.UpdatedBy = currentUser;
            order.UpdatedAt = DateTime.UtcNow;

            var updatedOrder = await _orderRepository.UpdateOrderAsync(order);

            _logger.Info("Order status updated", null, new {
                orderId = id,
                orderNumber = updatedOrder.OrderNumber,
                oldStatus = oldStatus,
                newStatus = updatedOrder.Status,
                updatedBy = currentUser
            });

            _logger.Business("ORDER_STATUS_UPDATED", null, new {
                orderId = id,
                orderNumber = updatedOrder.OrderNumber,
                oldStatus = oldStatus,
                newStatus = updatedOrder.Status
            });

            return MapToOrderResponseDto(updatedOrder);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to update order status for {id}", ex, null, new {
                orderId = id,
                newStatus = updateStatusDto.Status
            });
            throw;
        }
    }

    /// <summary>
    /// Get orders by status
    /// </summary>
    public async Task<IEnumerable<OrderResponseDto>> GetOrdersByStatusAsync(OrderStatus status)
    {
        _logger.Info("Getting orders by status", null, new { status });
        
        try
        {
            var orders = await _orderRepository.GetOrdersByStatusAsync(status);
            var orderDtos = orders.Select(MapToOrderResponseDto).ToList();
            
            _logger.Info("Retrieved orders by status", null, new { status, orderCount = orderDtos.Count });
            
            return orderDtos;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to get orders by status {status}", ex, null, new { status });
            throw;
        }
    }

    /// <summary>
    /// Delete an order
    /// </summary>
    public async Task<bool> DeleteOrderAsync(Guid id)
    {
        _logger.Info("Deleting order", null, new { orderId = id });
        
        try
        {
            var result = await _orderRepository.DeleteOrderAsync(id);
            
            if (result)
            {
                _logger.Info("Order deleted", null, new { orderId = id, deleted = true });
                
                _logger.Business("ORDER_DELETED", null, new { orderId = id });
            }
            else
            {
                _logger.Warn($"Failed to delete order: {id}", null, new { orderId = id });
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to delete order {id}", ex, null, new { orderId = id });
            throw;
        }
    }

    /// <summary>
    /// Get orders with pagination and filtering
    /// </summary>
    public async Task<PagedResponseDto<OrderResponseDto>> GetOrdersPagedAsync(OrderQueryDto query)
    {
        _logger.Info("Getting paged orders", null, new {
            page = query.Page,
            pageSize = query.PageSize,
            status = query.Status,
            customerId = query.CustomerId
        });
        
        try
        {
            var (orders, totalCount) = await _orderRepository.GetOrdersPagedAsync(query);
            var orderDtos = orders.Select(MapToOrderResponseDto).ToList();

            _logger.Info("Retrieved paged orders", null, new {
                page = query.Page,
                pageSize = query.PageSize,
                returnedCount = orderDtos.Count,
                totalCount = totalCount
            });

            return PagedResponseDto<OrderResponseDto>.Create(orderDtos, query.Page, query.PageSize, totalCount);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to get paged orders", ex, null, new {
                page = query.Page,
                pageSize = query.PageSize,
                status = query.Status,
                customerId = query.CustomerId
            });
            throw;
        }
    }

    /// <summary>
    /// Get orders by customer ID with pagination
    /// </summary>
    public async Task<PagedResponseDto<OrderResponseDto>> GetOrdersByCustomerIdPagedAsync(string customerId, PagedRequestDto pageRequest)
    {
        _logger.Info("Getting paged orders by customer", null, new {
            customerId,
            page = pageRequest.Page,
            pageSize = pageRequest.PageSize
        });
        
        try
        {
            var (orders, totalCount) = await _orderRepository.GetOrdersByCustomerIdPagedAsync(customerId, pageRequest);
            var orderDtos = orders.Select(MapToOrderResponseDto).ToList();

            _logger.Info("Retrieved paged orders for customer", null, new {
                customerId,
                page = pageRequest.Page,
                pageSize = pageRequest.PageSize,
                returnedCount = orderDtos.Count,
                totalCount = totalCount
            });

            return PagedResponseDto<OrderResponseDto>.Create(orderDtos, pageRequest.Page, pageRequest.PageSize, totalCount);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to get paged orders for customer {customerId}", ex, null, new {
                customerId,
                page = pageRequest.Page,
                pageSize = pageRequest.PageSize
            });
            throw;
        }
    }

    /// <summary>
    /// Generate order number using configuration
    /// </summary>
    private string GenerateOrderNumber()
    {
        return $"{ORDER_NUMBER_PREFIX}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    /// <summary>
    /// Map Order entity to OrderResponseDto
    /// </summary>
    private static OrderResponseDto MapToOrderResponseDto(Order order)
    {
        return new OrderResponseDto
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            CustomerPhone = order.CustomerPhone,
            OrderNumber = order.OrderNumber,
            Status = order.Status,
            PaymentStatus = order.PaymentStatus,
            ShippingStatus = order.ShippingStatus,
            Subtotal = order.Subtotal,
            TaxAmount = order.TaxAmount,
            ShippingCost = order.ShippingCost,
            DiscountAmount = order.DiscountAmount,
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt,
            CreatedBy = order.CreatedBy,
            UpdatedBy = order.UpdatedBy,
            Items = order.Items.Select(item => new OrderItemResponseDto
            {
                Id = item.Id,
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                ProductImageUrl = item.ProductImageUrl,
                ProductSku = item.ProductSku,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                TotalPrice = item.TotalPrice,
                DiscountAmount = item.DiscountAmount,
                TaxAmount = item.TaxAmount,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            }).ToList(),
            ShippingAddress = new AddressDto
            {
                AddressLine1 = order.ShippingAddress.AddressLine1,
                AddressLine2 = order.ShippingAddress.AddressLine2,
                City = order.ShippingAddress.City,
                State = order.ShippingAddress.State,
                ZipCode = order.ShippingAddress.ZipCode,
                Country = order.ShippingAddress.Country
            },
            BillingAddress = new AddressDto
            {
                AddressLine1 = order.BillingAddress.AddressLine1,
                AddressLine2 = order.BillingAddress.AddressLine2,
                City = order.BillingAddress.City,
                State = order.BillingAddress.State,
                ZipCode = order.BillingAddress.ZipCode,
                Country = order.BillingAddress.Country
            }
        };
    }

    /// <summary>
    /// Get order statistics for admin dashboard
    /// </summary>
    public async Task<OrderStatsDto> GetStatsAsync(bool includeRecent = false, int recentLimit = 10)
    {
        _logger.Info("Computing order statistics from database", null, new { includeRecent, recentLimit });

        try
        {
            // Get all orders (consider adding caching for large datasets)
            var allOrders = await _orderRepository.GetAllOrdersAsync();

            // Calculate date boundaries
            var now = DateTime.UtcNow;
            var firstDayThisMonth = new DateTime(now.Year, now.Month, 1);
            var firstDayLastMonth = firstDayThisMonth.AddMonths(-1);
            var lastDayLastMonth = firstDayThisMonth.AddDays(-1);

            // Aggregate statistics
            var total = allOrders.Count();
            var pending = allOrders.Count(o => o.Status == OrderStatus.Created || o.Status == OrderStatus.Confirmed || o.Status == OrderStatus.Processing);
            var completed = allOrders.Count(o => o.Status == OrderStatus.Delivered);
            var newThisMonth = allOrders.Count(o => o.CreatedAt >= firstDayThisMonth);
            var newLastMonth = allOrders.Count(o => o.CreatedAt >= firstDayLastMonth && o.CreatedAt < firstDayThisMonth);
            var revenue = allOrders
                .Where(o => o.Status == OrderStatus.Delivered || o.Status == OrderStatus.Shipped)
                .Sum(o => o.TotalAmount);

            // Calculate growth percentage
            var growth = newLastMonth > 0
                ? ((decimal)(newThisMonth - newLastMonth) / newLastMonth) * 100
                : newThisMonth > 0
                    ? 100
                    : 0;

            var stats = new OrderStatsDto
            {
                Total = total,
                Pending = pending,
                Completed = completed,
                NewThisMonth = newThisMonth,
                Growth = Math.Round(growth, 1),
                Revenue = Math.Round(revenue, 2)
            };

            // Add recent orders if requested
            if (includeRecent)
            {
                stats.RecentOrders = allOrders
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(recentLimit)
                    .Select(o => new RecentOrderDto
                    {
                        Id = o.Id.ToString(),
                        OrderNumber = o.OrderNumber,
                        CustomerId = o.CustomerId,
                        CustomerName = o.CustomerName,
                        Status = o.Status.ToString(),
                        TotalAmount = o.TotalAmount,
                        ItemCount = o.Items.Count,
                        CreatedAt = o.CreatedAt
                    })
                    .ToList();
            }

            _logger.Info("Order statistics computed successfully", null, new {
                total,
                pending,
                completed,
                revenue,
                newThisMonth,
                includeRecent,
                recentOrdersCount = stats.RecentOrders?.Count() ?? 0
            });

            return stats;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to compute order statistics", ex, null, new {
                includeRecent,
                recentLimit
            });
            throw;
        }
    }

    /// <summary>
    /// Map Order entity to OrderCreatedEvent for messaging
    /// </summary>
    private static OrderCreatedEvent MapToOrderCreatedEvent(Order order, string correlationId)
    {
        return new OrderCreatedEvent
        {
            OrderId = order.Id,
            CorrelationId = correlationId,
            CustomerId = order.CustomerId,
            OrderNumber = order.OrderNumber,
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            CreatedAt = order.CreatedAt,
            Items = order.Items.Select(item => new OrderItemEvent
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice
            }).ToList(),
            ShippingAddress = new AddressEvent
            {
                AddressLine1 = order.ShippingAddress.AddressLine1,
                AddressLine2 = order.ShippingAddress.AddressLine2,
                City = order.ShippingAddress.City,
                State = order.ShippingAddress.State,
                ZipCode = order.ShippingAddress.ZipCode,
                Country = order.ShippingAddress.Country
            },
            BillingAddress = new AddressEvent
            {
                AddressLine1 = order.BillingAddress.AddressLine1,
                AddressLine2 = order.BillingAddress.AddressLine2,
                City = order.BillingAddress.City,
                State = order.BillingAddress.State,
                ZipCode = order.BillingAddress.ZipCode,
                Country = order.BillingAddress.Country
            }
        };
    }

    /// <summary>
    /// Cancel an order and trigger saga compensation
    /// </summary>
    public async Task<OrderResponseDto?> CancelOrderAsync(Guid id, CancelOrderDto cancelOrderDto, string correlationId = "")
    {
        var currentCorrelationId = !string.IsNullOrEmpty(correlationId) ? correlationId : Guid.NewGuid().ToString();
        var currentUser = _currentUserService.GetUserName() ?? _currentUserService.GetUserId() ?? "System";

        _logger.Info("Cancelling order", currentCorrelationId, new { orderId = id, cancelledBy = currentUser });

        try
        {
            var order = await _orderRepository.GetOrderByIdAsync(id);
            if (order == null)
            {
                _logger.Warn($"Order with ID {id} not found", currentCorrelationId, new { orderId = id });
                return null;
            }

            // Validate order can be cancelled
            var validationError = ValidateOrderCancellation(order);
            if (validationError != null)
            {
                _logger.Warn($"Order cancellation validation failed: {validationError}", currentCorrelationId, new {
                    orderId = id,
                    orderNumber = order.OrderNumber,
                    currentStatus = order.Status,
                    error = validationError
                });
                throw new InvalidOperationException(validationError);
            }

            // Update order status and cancellation details
            var oldStatus = order.Status;
            order.Status = OrderStatus.Cancelled;
            order.CancelledDate = DateTime.UtcNow;
            order.CancellationReason = cancelOrderDto.CancellationReason;
            order.CancelledBy = currentUser;
            order.UpdatedBy = currentUser;
            order.UpdatedAt = DateTime.UtcNow;

            var cancelledOrder = await _orderRepository.UpdateOrderAsync(order);

            _logger.Info("Order cancelled successfully", currentCorrelationId, new {
                orderId = id,
                orderNumber = cancelledOrder.OrderNumber,
                oldStatus = oldStatus,
                cancelledBy = currentUser
            });

            _logger.Business("ORDER_CANCELLED", currentCorrelationId, new {
                orderId = id,
                orderNumber = cancelledOrder.OrderNumber,
                totalAmount = cancelledOrder.TotalAmount,
                reason = cancelOrderDto.CancellationReason
            });

            // Publish order.cancelled event for saga compensation
            try
            {
                var orderCancelledEvent = MapToOrderCancelledEvent(cancelledOrder, currentCorrelationId, currentUser);
                await _messagingProvider.PublishEventAsync(
                    "order.cancelled",
                    orderCancelledEvent,
                    currentCorrelationId);

                _logger.Info("Published order.cancelled event", currentCorrelationId, new {
                    orderNumber = cancelledOrder.OrderNumber,
                    requiresPaymentRefund = orderCancelledEvent.RequiresPaymentRefund,
                    requiresInventoryRelease = orderCancelledEvent.RequiresInventoryRelease
                });
            }
            catch (Exception eventEx)
            {
                // Log error but don't fail the cancellation (event will be retried)
                _logger.Error("Failed to publish order.cancelled event", eventEx, currentCorrelationId, new {
                    orderNumber = cancelledOrder.OrderNumber,
                    error = eventEx.Message
                });
            }

            return MapToOrderResponseDto(cancelledOrder);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to cancel order {id}", ex, currentCorrelationId, new { orderId = id });
            throw;
        }
    }

    /// <summary>
    /// Validate if an order can be cancelled
    /// </summary>
    private static string? ValidateOrderCancellation(Order order)
    {
        // Already cancelled
        if (order.Status == OrderStatus.Cancelled)
        {
            return "Order is already cancelled";
        }

        // Already refunded
        if (order.Status == OrderStatus.Refunded)
        {
            return "Order has already been refunded and cannot be cancelled";
        }

        // Already delivered (should use return/refund process instead)
        if (order.Status == OrderStatus.Delivered)
        {
            return "Order has already been delivered. Please use the return process instead";
        }

        // Shipped orders can still be cancelled but may incur return shipping costs
        // Business rule: Allow cancellation at any stage before delivery
        return null;
    }

    /// <summary>
    /// Map Order entity to OrderCancelledEvent for saga compensation
    /// </summary>
    private static OrderCancelledEvent MapToOrderCancelledEvent(Order order, string correlationId, string cancelledBy)
    {
        return new OrderCancelledEvent
        {
            OrderId = order.Id,
            CorrelationId = correlationId,
            CustomerId = order.CustomerId,
            OrderNumber = order.OrderNumber,
            CancellationReason = order.CancellationReason ?? "No reason provided",
            CancelledBy = cancelledBy,
            CancelledAt = order.CancelledDate ?? DateTime.UtcNow,
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            PaymentTransactionId = order.PaymentTransactionId,
            PaymentProvider = order.PaymentProvider,
            Items = order.Items.Select(item => new OrderItemEvent
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice
            }).ToList(),
            // Determine if compensation is needed based on order state
            RequiresPaymentRefund = !string.IsNullOrEmpty(order.PaymentTransactionId) && 
                                   order.PaymentStatus != PaymentStatus.Failed &&
                                   order.PaymentStatus != PaymentStatus.Cancelled,
            RequiresInventoryRelease = order.Status == OrderStatus.Processing || 
                                      order.Status == OrderStatus.Confirmed ||
                                      order.Status == OrderStatus.Shipped
        };
    }

    /// <summary>
    /// Update order tracking information and publish tracking event
    /// </summary>
    public async Task<OrderResponseDto?> UpdateTrackingAsync(Guid id, UpdateTrackingDto updateTrackingDto, string correlationId = "")
    {
        var currentCorrelationId = !string.IsNullOrEmpty(correlationId) ? correlationId : Guid.NewGuid().ToString();
        var currentUser = _currentUserService.GetUserName() ?? _currentUserService.GetUserId() ?? "System";

        _logger.Info("Updating order tracking", currentCorrelationId, new {
            orderId = id,
            carrier = updateTrackingDto.CarrierName,
            trackingNumber = updateTrackingDto.TrackingNumber,
            updatedBy = currentUser
        });

        try
        {
            var order = await _orderRepository.GetOrderByIdAsync(id);
            if (order == null)
            {
                _logger.Warn($"Order with ID {id} not found", currentCorrelationId, new { orderId = id });
                return null;
            }

            // Validate order can be tracked (not cancelled)
            if (order.Status == OrderStatus.Cancelled)
            {
                _logger.Warn("Cannot update tracking for cancelled order", currentCorrelationId, new {
                    orderId = id,
                    orderNumber = order.OrderNumber
                });
                throw new InvalidOperationException("Cannot update tracking information for cancelled orders");
            }

            // Update tracking information
            order.CarrierName = updateTrackingDto.CarrierName;
            order.TrackingNumber = updateTrackingDto.TrackingNumber;
            order.ShippedDate = DateTime.UtcNow;
            order.EstimatedDeliveryDate = updateTrackingDto.EstimatedDeliveryDate;
            order.UpdatedBy = currentUser;
            order.UpdatedAt = DateTime.UtcNow;

            // Update shipping status to Shipped if not already
            if (order.ShippingStatus != ShippingStatus.Shipped && order.ShippingStatus != ShippingStatus.Delivered)
            {
                order.ShippingStatus = ShippingStatus.Shipped;
            }

            // Update order status to Shipped if it was processing
            if (order.Status == OrderStatus.Processing || order.Status == OrderStatus.Confirmed)
            {
                order.Status = OrderStatus.Shipped;
            }

            var updatedOrder = await _orderRepository.UpdateOrderAsync(order);

            _logger.Info("Order tracking updated successfully", currentCorrelationId, new {
                orderId = id,
                orderNumber = updatedOrder.OrderNumber,
                carrier = updatedOrder.CarrierName,
                trackingNumber = updatedOrder.TrackingNumber
            });

            _logger.Business("ORDER_SHIPPED", currentCorrelationId, new {
                orderId = id,
                orderNumber = updatedOrder.OrderNumber,
                carrier = updatedOrder.CarrierName,
                estimatedDelivery = updatedOrder.EstimatedDeliveryDate
            });

            // Publish order.shipped event for notifications and analytics
            try
            {
                var orderShippedEvent = MapToOrderShippedEvent(updatedOrder, currentCorrelationId);
                await _messagingProvider.PublishEventAsync(
                    "order.shipped",
                    orderShippedEvent,
                    currentCorrelationId);

                _logger.Info("Published order.shipped event", currentCorrelationId, new {
                    orderNumber = updatedOrder.OrderNumber,
                    trackingNumber = updatedOrder.TrackingNumber
                });
            }
            catch (Exception eventEx)
            {
                // Log error but don't fail the tracking update
                _logger.Error("Failed to publish order.shipped event", eventEx, currentCorrelationId, new {
                    orderNumber = updatedOrder.OrderNumber,
                    error = eventEx.Message
                });
            }

            return MapToOrderResponseDto(updatedOrder);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to update tracking for order {id}", ex, currentCorrelationId, new { orderId = id });
            throw;
        }
    }

    /// <summary>
    /// Get order tracking information with timeline
    /// </summary>
    public async Task<TrackingInfoDto?> GetTrackingInfoAsync(Guid id)
    {
        _logger.Info($"Getting tracking info for order: {id}", null, new { orderId = id });

        try
        {
            var order = await _orderRepository.GetOrderByIdAsync(id);
            if (order == null)
            {
                _logger.Warn($"Order with ID {id} not found", null, new { orderId = id });
                return null;
            }

            // Build tracking timeline from order history
            var timeline = BuildTrackingTimeline(order);

            var trackingInfo = new TrackingInfoDto
            {
                CarrierName = order.CarrierName,
                TrackingNumber = order.TrackingNumber,
                TrackingUrl = order.TrackingNumber != null && order.CarrierName != null
                    ? GenerateTrackingUrl(order.CarrierName, order.TrackingNumber)
                    : null,
                ShippedDate = order.ShippedDate,
                EstimatedDeliveryDate = order.EstimatedDeliveryDate,
                DeliveredDate = order.DeliveredDate,
                ShippingStatus = order.ShippingStatus.ToString(),
                Timeline = timeline
            };

            return trackingInfo;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to get tracking info for order {id}", ex, null, new { orderId = id });
            throw;
        }
    }

    /// <summary>
    /// Build tracking timeline from order status history
    /// </summary>
    private static List<TrackingEventDto> BuildTrackingTimeline(Order order)
    {
        var timeline = new List<TrackingEventDto>();

        // Order Created
        timeline.Add(new TrackingEventDto
        {
            Status = "Created",
            Description = "Order placed successfully",
            Timestamp = order.CreatedAt,
            IsCompleted = true
        });

        // Order Confirmed
        if (order.Status != OrderStatus.Created)
        {
            timeline.Add(new TrackingEventDto
            {
                Status = "Confirmed",
                Description = "Order confirmed and being prepared",
                Timestamp = order.UpdatedAt,
                IsCompleted = order.Status != OrderStatus.Created
            });
        }

        // Order Shipped
        if (order.ShippedDate.HasValue)
        {
            timeline.Add(new TrackingEventDto
            {
                Status = "Shipped",
                Description = $"Package shipped with {order.CarrierName}",
                Timestamp = order.ShippedDate.Value,
                IsCompleted = true
            });
        }

        // Estimated Delivery
        if (order.EstimatedDeliveryDate.HasValue)
        {
            timeline.Add(new TrackingEventDto
            {
                Status = "In Transit",
                Description = "Package is on the way",
                Timestamp = order.EstimatedDeliveryDate.Value.AddDays(-1),
                IsCompleted = order.DeliveredDate.HasValue
            });
        }

        // Order Delivered
        if (order.DeliveredDate.HasValue)
        {
            timeline.Add(new TrackingEventDto
            {
                Status = "Delivered",
                Description = "Package delivered successfully",
                Timestamp = order.DeliveredDate.Value,
                IsCompleted = true
            });
        }
        else if (order.EstimatedDeliveryDate.HasValue)
        {
            // Add expected delivery as pending
            timeline.Add(new TrackingEventDto
            {
                Status = "Out for Delivery",
                Description = "Expected delivery",
                Timestamp = order.EstimatedDeliveryDate.Value,
                IsCompleted = false
            });
        }

        return timeline.OrderBy(t => t.Timestamp).ToList();
    }

    /// <summary>
    /// Generate tracking URL based on carrier
    /// </summary>
    private static string? GenerateTrackingUrl(string carrierName, string trackingNumber)
    {
        return carrierName.ToLower() switch
        {
            "ups" => $"https://www.ups.com/track?tracknum={trackingNumber}",
            "fedex" => $"https://www.fedex.com/fedextrack/?tracknumbers={trackingNumber}",
            "usps" => $"https://tools.usps.com/go/TrackConfirmAction?tLabels={trackingNumber}",
            "dhl" => $"https://www.dhl.com/en/express/tracking.html?AWB={trackingNumber}",
            _ => null
        };
    }

    /// <summary>
    /// Map Order entity to OrderShippedEvent for notifications
    /// </summary>
    private static OrderShippedEvent MapToOrderShippedEvent(Order order, string correlationId)
    {
        return new OrderShippedEvent
        {
            OrderId = order.Id,
            CorrelationId = correlationId,
            CustomerId = order.CustomerId,
            OrderNumber = order.OrderNumber,
            CustomerEmail = order.CustomerEmail,
            CustomerName = order.CustomerName,
            CarrierName = order.CarrierName ?? string.Empty,
            TrackingNumber = order.TrackingNumber ?? string.Empty,
            TrackingUrl = GenerateTrackingUrl(order.CarrierName ?? "", order.TrackingNumber ?? ""),
            ShippedDate = order.ShippedDate ?? DateTime.UtcNow,
            EstimatedDeliveryDate = order.EstimatedDeliveryDate,
            Items = order.Items.Select(item => new OrderItemEvent
            {
                ProductId = item.ProductId,
                ProductName = item.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = item.TotalPrice
            }).ToList(),
            ShippingAddress = new AddressEvent
            {
                AddressLine1 = order.ShippingAddress.AddressLine1,
                AddressLine2 = order.ShippingAddress.AddressLine2,
                City = order.ShippingAddress.City,
                State = order.ShippingAddress.State,
                ZipCode = order.ShippingAddress.ZipCode,
                Country = order.ShippingAddress.Country
            }
        };
    }
}
