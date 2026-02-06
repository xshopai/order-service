using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using OrderService.Core.Models.DTOs;
using OrderService.Core.Models.Enums;
using OrderService.Core.Models.Events;
using OrderService.Core.Services;
using OrderService.Core.Configuration;
using OrderService.Core.Utils;
using System.Diagnostics;
namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Require authentication for all endpoints
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly StandardLogger _logger;

    public OrdersController(
        IOrderService orderService, 
        StandardLogger logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// Get order by ID (Customer can view own orders, Admin can view all)
    /// </summary>
    /// <route>GET /api/orders/{id}</route>
    [HttpGet("{id}")]
    [Authorize(Policy = "CustomerOrAdmin")]
    public async Task<ActionResult<OrderResponseDto>> GetOrder(Guid id)
    {
        var correlationId = GetCorrelationId();
        var currentUserId = GetCurrentUserId();
        var isAdmin = IsCurrentUserAdmin();
        
        
        try
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            
            if (order == null)
            {
                _logger.Warn($"Order with ID {id} not found", correlationId, new {
                    orderId = id,
                    endpoint = "GET /api/orders/{id}"
                });
                return NotFound($"Order with ID {id} not found");
            }

            // Check if customer is trying to access their own order
            if (!isAdmin && currentUserId != order.CustomerId)
            {
                _logger.Info("UNAUTHORIZED_ORDER_ACCESS_ATTEMPT", correlationId, new {
                    orderId = id,
                    requestedBy = currentUserId,
                    orderOwner = order.CustomerId,
                    endpoint = "GET /api/orders/{id}"
                });
                return StatusCode(403, new { message = "You can only view your own orders" });
            }

            

            return Ok(order);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error fetching order {id}", ex, correlationId);
            return StatusCode(500, "An error occurred while fetching the order");
        }
    }

    /// <summary>
    /// Create a new order (Customer only)
    /// </summary>
    /// <route>POST /api/orders</route>
    [HttpPost]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<ActionResult<OrderResponseDto>> CreateOrder(CreateOrderDto createOrderDto)
    {
        // DEBUG: Log all headers
        _logger.Info($"CreateOrder - Received headers: {string.Join(", ", Request.Headers.Select(h => $"{h.Key}={h.Value}"))}", null, null);
        _logger.Info($"CreateOrder - User.Identity.IsAuthenticated: {User.Identity?.IsAuthenticated}", null, null);
        _logger.Info($"CreateOrder - User.Claims: {string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}"))}", null, null);
        
        var correlationId = GetCorrelationId();
        var customerId = GetCurrentUserId();
       

        try
        {
            // Ensure the customer can only create orders for themselves
            if (customerId != createOrderDto.CustomerId)
            {
                
                return StatusCode(403, new { 
                    message = "You can only create orders for yourself",
                    requestedCustomerId = createOrderDto.CustomerId,
                    authenticatedCustomerId = customerId
                });
            }

            var order = await _orderService.CreateOrderAsync(createOrderDto, correlationId);
                       

            _logger.Info("ORDER_CREATED", correlationId, new {
                orderId = order.Id,
                customerId = order.CustomerId,
                totalAmount = order.TotalAmount,
                orderNumber = order.OrderNumber
            });

            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
        }
        catch (Exception ex)
        {
            _logger.Error("Error creating order", ex, correlationId);
            return StatusCode(500, "An error occurred while creating the order");
        }
    }

    /// <summary>
    /// Get orders by customer ID (Customer can view own orders, Admin can view all)
    /// </summary>
    /// <route>GET /api/orders/customer/{customerId}</route>
    [HttpGet("customer/{customerId}")]
    [Authorize(Policy = "CustomerOrAdmin")]
    public async Task<ActionResult<IEnumerable<OrderResponseDto>>> GetOrdersByCustomerId(string customerId)
    {
        try
        {
            var customerIdFromToken = GetCurrentUserId();
            var isAdmin = IsCurrentUserAdmin();
            
            // Customers can only view their own orders, admins can view any customer's orders
            if (!isAdmin && customerIdFromToken != customerId)
            {
                return StatusCode(403, new { message = "You can only view your own orders" });
            }

            var orders = await _orderService.GetOrdersByCustomerIdAsync(customerId);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error fetching orders for customer: {customerId}", ex, GetCorrelationId());
            return StatusCode(500, "An error occurred while fetching orders");
        }
    }

    /// <summary>
    /// Get orders by customer ID with pagination (Customer can view own orders, Admin can view all)
    /// </summary>
    /// <route>GET /api/orders/customer/{customerId}/paged</route>
    [HttpGet("customer/{customerId}/paged")]
    [Authorize(Policy = "CustomerOrAdmin")]
    public async Task<ActionResult<PagedResponseDto<OrderResponseDto>>> GetOrdersByCustomerIdPaged(
        string customerId, 
        [FromQuery] PagedRequestDto pageRequest)
    {
        try
        {
            var customerIdFromToken = GetCurrentUserId();
            var isAdmin = IsCurrentUserAdmin();
            
            // Customers can only view their own orders, admins can view any customer's orders
            if (!isAdmin && customerIdFromToken != customerId)
            {
                return StatusCode(403, new { message = "You can only view your own orders" });
            }

            var pagedOrders = await _orderService.GetOrdersByCustomerIdPagedAsync(customerId, pageRequest);
            return Ok(pagedOrders);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error fetching paged orders for customer: {customerId}", ex, GetCorrelationId());
            return StatusCode(500, "An error occurred while fetching orders");
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
        // Use standard ClaimTypes.NameIdentifier which the JWT middleware maps 'sub' to
        return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? 
               User.FindFirst("sub")?.Value ?? 
               User.FindFirst("id")?.Value;
    }
    
    /// <summary>
    /// Helper method to check if current user is admin
    /// </summary>
    private bool IsCurrentUserAdmin()
    {
        // Check standard ClaimTypes.Role or custom 'roles' claim
        return User.HasClaim(System.Security.Claims.ClaimTypes.Role, "admin") || 
               User.HasClaim("roles", "admin") ||
               User.HasClaim("role", "admin");
    }
}
