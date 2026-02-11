using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using OrderService.Core.Models.DTOs;
using OrderService.Core.Models.Enums;
using OrderService.Core.Services;
using OrderService.Core.Utils;

namespace OrderService.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // Require authentication for all endpoints
public class ReturnsController : ControllerBase
{
    private readonly IOrderReturnService _returnService;
    private readonly StandardLogger _logger;

    public ReturnsController(
        IOrderReturnService returnService,
        StandardLogger logger)
    {
        _returnService = returnService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new return request (Customer only)
    /// </summary>
    /// <route>POST /api/returns</route>
    [HttpPost]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<ActionResult<ReturnResponseDto>> CreateReturn(CreateReturnRequestDto createReturnDto)
    {
        var correlationId = GetCorrelationId();
        var currentUserId = GetCurrentUserId();

        _logger.Info("Creating return request", correlationId, new
        {
            orderId = createReturnDto.OrderId,
            itemCount = createReturnDto.Items?.Count ?? 0,
            reason = createReturnDto.Reason.ToString(),
            requestedBy = currentUserId,
            endpoint = "POST /api/returns"
        });

        try
        {
            var returnDto = await _returnService.CreateReturnAsync(createReturnDto, currentUserId, correlationId);

            _logger.Info("RETURN_CREATED", correlationId, new
            {
                returnId = returnDto.Id,
                returnNumber = returnDto.ReturnNumber,
                orderId = returnDto.OrderId,
                customerId = currentUserId,
                totalRefund = returnDto.TotalRefund,
                endpoint = "POST /api/returns"
            });

            return CreatedAtAction(nameof(GetReturn), new { id = returnDto.Id }, returnDto);
        }
        catch (InvalidOperationException ex)
        {
            _logger.Warn($"Invalid return request: {ex.Message}", correlationId, new
            {
                orderId = createReturnDto.OrderId,
                error = ex.Message
            });
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to create return request", ex, correlationId, new
            {
                orderId = createReturnDto.OrderId
            });
            return StatusCode(500, "An error occurred while creating the return request");
        }
    }

    /// <summary>
    /// Get return by ID (Customer can view own returns, Admin can view all)
    /// </summary>
    /// <route>GET /api/returns/{id}</route>
    [HttpGet("{id}")]
    [Authorize(Policy = "CustomerOrAdmin")]
    public async Task<ActionResult<ReturnResponseDto>> GetReturn(Guid id)
    {
        var correlationId = GetCorrelationId();
        var currentUserId = GetCurrentUserId();
        var isAdmin = IsCurrentUserAdmin();

        try
        {
            var returnDto = await _returnService.GetReturnByIdAsync(id);

            if (returnDto == null)
            {
                _logger.Warn($"Return with ID {id} not found", correlationId, new
                {
                    returnId = id,
                    endpoint = "GET /api/returns/{id}"
                });
                return NotFound($"Return with ID {id} not found");
            }

            // Check if customer is trying to access their own return
            if (!isAdmin && currentUserId != returnDto.CustomerId)
            {
                _logger.Info("UNAUTHORIZED_RETURN_ACCESS_ATTEMPT", correlationId, new
                {
                    returnId = id,
                    requestedBy = currentUserId,
                    returnOwner = returnDto.CustomerId,
                    endpoint = "GET /api/returns/{id}"
                });
                return StatusCode(403, new { message = "You can only view your own returns" });
            }

            return Ok(returnDto);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error fetching return {id}", ex, correlationId);
            return StatusCode(500, "An error occurred while fetching the return");
        }
    }

    /// <summary>
    /// Get returns for current customer
    /// </summary>
    /// <route>GET /api/returns/my</route>
    [HttpGet("my")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<ActionResult<IEnumerable<ReturnResponseDto>>> GetMyReturns()
    {
        var correlationId = GetCorrelationId();
        var currentUserId = GetCurrentUserId();

        _logger.Info("Getting returns for current customer", correlationId, new
        {
            customerId = currentUserId,
            endpoint = "GET /api/returns/my"
        });

        try
        {
            var returns = await _returnService.GetReturnsByCustomerIdAsync(currentUserId);

            return Ok(returns);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error fetching returns for customer {currentUserId}", ex, correlationId);
            return StatusCode(500, "An error occurred while fetching your returns");
        }
    }

    /// <summary>
    /// Get returns by order ID (Customer can view own order returns, Admin can view all)
    /// </summary>
    /// <route>GET /api/returns/order/{orderId}</route>
    [HttpGet("order/{orderId}")]
    [Authorize(Policy = "CustomerOrAdmin")]
    public async Task<ActionResult<IEnumerable<ReturnResponseDto>>> GetReturnsByOrder(Guid orderId)
    {
        var correlationId = GetCorrelationId();
        var currentUserId = GetCurrentUserId();

        _logger.Info($"Getting returns for order {orderId}", correlationId, new
        {
            orderId,
            requestedBy = currentUserId,
            endpoint = "GET /api/returns/order/{orderId}"
        });

        try
        {
            var returns = await _returnService.GetReturnsByOrderIdAsync(orderId);

            return Ok(returns);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error fetching returns for order {orderId}", ex, correlationId);
            return StatusCode(500, "An error occurred while fetching returns for the order");
        }
    }

    /// <summary>
    /// Check if an order is eligible for return
    /// </summary>
    /// <route>GET /api/returns/eligibility/{orderId}</route>
    [HttpGet("eligibility/{orderId}")]
    [Authorize(Policy = "CustomerOnly")]
    public async Task<ActionResult<object>> CheckReturnEligibility(Guid orderId)
    {
        var correlationId = GetCorrelationId();
        var currentUserId = GetCurrentUserId();

        _logger.Info($"Checking return eligibility for order {orderId}", correlationId, new
        {
            orderId,
            requestedBy = currentUserId,
            endpoint = "GET /api/returns/eligibility/{orderId}"
        });

        try
        {
            var (isEligible, reason) = await _returnService.IsOrderEligibleForReturnAsync(orderId);

            return Ok(new
            {
                orderId,
                isEligible,
                reason = reason ?? "Order is eligible for return"
            });
        }
        catch (Exception ex)
        {
            _logger.Error($"Error checking eligibility for order {orderId}", ex, correlationId);
            return StatusCode(500, "An error occurred while checking return eligibility");
        }
    }

    // Helper methods
    private string GetCorrelationId()
    {
        return Request.Headers.TryGetValue("X-Correlation-ID", out var correlationId)
            ? correlationId.ToString()
            : Guid.NewGuid().ToString();
    }

    private string GetCurrentUserId()
    {
        return User.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? string.Empty;
    }

    private bool IsCurrentUserAdmin()
    {
        return User.Claims.Any(c => c.Type == "role" && c.Value == "admin");
    }
}
