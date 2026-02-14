using Microsoft.AspNetCore.Mvc;
using OrderService.Core.Models.DTOs;
using OrderService.Core.Services;
using OrderService.Core.Utils;

namespace OrderService.Controllers;

/// <summary>
/// Internal API controller for service-to-service communication.
/// These endpoints are not exposed to external clients and are 
/// called by other services via Dapr service invocation.
/// </summary>
[ApiController]
[Route("api/v1/internal/orders")]
public class InternalController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly StandardLogger _logger;

    public InternalController(
        IOrderService orderService,
        StandardLogger logger)
    {
        _orderService = orderService;
        _logger = logger;
    }

    /// <summary>
    /// Validate if a user made a purchase of a product.
    /// Used by review-service to verify purchase before allowing verified reviews.
    /// </summary>
    /// <route>POST /api/v1/internal/orders/validate-purchase</route>
    /// <remarks>
    /// This endpoint is called via Dapr service invocation from review-service.
    /// No authentication required as it's internal service-to-service communication.
    /// </remarks>
    [HttpPost("validate-purchase")]
    public async Task<ActionResult<ValidatePurchaseResponseDto>> ValidatePurchase(
        [FromBody] ValidatePurchaseRequestDto request)
    {
        var correlationId = GetCorrelationId();

        _logger.Info("Internal API: Validate purchase request received", correlationId, new {
            endpoint = "POST /api/v1/internal/orders/validate-purchase",
            userId = request.UserId,
            productId = request.ProductId,
            orderReference = request.OrderReference
        });

        try
        {
            var result = await _orderService.ValidatePurchaseAsync(request, correlationId);

            _logger.Info("Internal API: Validate purchase completed", correlationId, new {
                isValid = result.IsValid,
                orderId = result.OrderId,
                orderNumber = result.OrderNumber
            });

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.Error("Internal API: Error validating purchase", ex, correlationId);
            return StatusCode(500, new ValidatePurchaseResponseDto
            {
                IsValid = false,
                Message = "Internal error validating purchase"
            });
        }
    }

    /// <summary>
    /// Check if an order exists by ID.
    /// Used for internal service communication.
    /// </summary>
    /// <route>GET /api/v1/internal/orders/{id}/exists</route>
    [HttpGet("{id}/exists")]
    public async Task<ActionResult<object>> CheckOrderExists(Guid id)
    {
        var correlationId = GetCorrelationId();

        try
        {
            var order = await _orderService.GetOrderByIdAsync(id);
            return Ok(new { exists = order != null });
        }
        catch (Exception ex)
        {
            _logger.Error($"Internal API: Error checking order existence {id}", ex, correlationId);
            return StatusCode(500, new { exists = false, error = "Internal error" });
        }
    }

    /// <summary>
    /// Helper method to get correlation ID from context
    /// </summary>
    private string GetCorrelationId()
    {
        return HttpContext.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();
    }
}
