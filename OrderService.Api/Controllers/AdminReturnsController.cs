using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using OrderService.Core.Models.DTOs;
using OrderService.Core.Models.Enums;
using OrderService.Core.Services;
using OrderService.Core.Utils;

namespace OrderService.Controllers;

/// <summary>
/// Admin-specific endpoints for return management
/// </summary>
[ApiController]
[Route("api/admin/returns")]
[Authorize(Policy = "AdminOnly")]
public class AdminReturnsController : ControllerBase
{
    private readonly IOrderReturnService _returnService;
    private readonly StandardLogger _logger;

    public AdminReturnsController(
        IOrderReturnService returnService,
        StandardLogger logger)
    {
        _returnService = returnService;
        _logger = logger;
    }

    /// <summary>
    /// Get all returns (Admin only)
    /// </summary>
    /// <route>GET /api/admin/returns</route>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReturnResponseDto>>> GetAllReturns()
    {
        var correlationId = GetCorrelationId();

        _logger.Info("Getting all returns", correlationId, new
        {
            endpoint = "GET /api/admin/returns"
        });

        try
        {
            var returns = await _returnService.GetAllReturnsAsync();

            return Ok(returns);
        }
        catch (Exception ex)
        {
            _logger.Error("Error fetching all returns", ex, correlationId);
            return StatusCode(500, "An error occurred while fetching returns");
        }
    }

    /// <summary>
    /// Get returns with pagination and filtering (Admin only)
    /// </summary>
    /// <route>GET /api/admin/returns/paged</route>
    /// <param name="status">Optional filter by status</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 20)</param>
    [HttpGet("paged")]
    public async Task<ActionResult<object>> GetReturnsPaged(
        [FromQuery] ReturnStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var correlationId = GetCorrelationId();

        _logger.Info("Getting paged returns", correlationId, new
        {
            endpoint = "GET /api/admin/returns/paged",
            status = status?.ToString() ?? "All",
            page,
            pageSize
        });

        try
        {
            var (returns, totalCount) = await _returnService.GetReturnsByStatusPagedAsync(status, page, pageSize);

            var result = new
            {
                data = returns,
                pagination = new
                {
                    page,
                    pageSize,
                    totalCount,
                    totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                }
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.Error("Error fetching paged returns", ex, correlationId);
            return StatusCode(500, "An error occurred while fetching returns");
        }
    }

    /// <summary>
    /// Get a single return by ID (Admin only)
    /// </summary>
    /// <route>GET /api/admin/returns/{id}</route>
    [HttpGet("{id}")]
    public async Task<ActionResult<ReturnResponseDto>> GetReturnById(Guid id)
    {
        var correlationId = GetCorrelationId();

        _logger.Info("Getting return by ID", correlationId, new
        {
            endpoint = "GET /api/admin/returns/{id}",
            returnId = id
        });

        try
        {
            var returnDto = await _returnService.GetReturnByIdAsync(id);

            if (returnDto == null)
            {
                _logger.Warn($"Return with ID {id} not found", correlationId, new
                {
                    returnId = id,
                    endpoint = "GET /api/admin/returns/{id}"
                });
                return NotFound($"Return with ID {id} not found");
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
    /// Update return status (Admin only)
    /// </summary>
    /// <route>PUT /api/admin/returns/{id}/status</route>
    [HttpPut("{id}/status")]
    public async Task<ActionResult<ReturnResponseDto>> UpdateReturnStatus(Guid id, UpdateReturnStatusDto updateStatusDto)
    {
        var correlationId = GetCorrelationId();
        var currentUserId = GetCurrentUserId();

        _logger.Info($"Updating return status: {id}", correlationId, new
        {
            returnId = id,
            newStatus = updateStatusDto.Status.ToString(),
            updatedBy = currentUserId,
            endpoint = "PUT /api/admin/returns/{id}/status"
        });

        try
        {
            var returnDto = await _returnService.UpdateReturnStatusAsync(id, updateStatusDto, currentUserId, correlationId);

            if (returnDto == null)
            {
                _logger.Warn($"Return with ID {id} not found", correlationId, new
                {
                    returnId = id,
                    endpoint = "PUT /api/admin/returns/{id}/status"
                });
                return NotFound($"Return with ID {id} not found");
            }

            _logger.Info("RETURN_STATUS_UPDATED", correlationId, new
            {
                returnId = id,
                returnNumber = returnDto.ReturnNumber,
                newStatus = returnDto.Status.ToString(),
                updatedBy = currentUserId,
                endpoint = "PUT /api/admin/returns/{id}/status"
            });

            return Ok(returnDto);
        }
        catch (InvalidOperationException ex)
        {
            _logger.Warn($"Invalid status update: {ex.Message}", correlationId, new
            {
                returnId = id,
                error = ex.Message
            });
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.Error($"Error updating return status: {id}", ex, correlationId);
            return StatusCode(500, "An error occurred while updating the return status");
        }
    }

    /// <summary>
    /// Get return statistics (Admin only)
    /// </summary>
    /// <route>GET /api/admin/returns/stats</route>
    [HttpGet("stats")]
    public async Task<ActionResult<Dictionary<string, int>>> GetReturnStatistics()
    {
        var correlationId = GetCorrelationId();

        _logger.Info("Getting return statistics", correlationId, new
        {
            endpoint = "GET /api/admin/returns/stats"
        });

        try
        {
            var stats = await _returnService.GetReturnStatisticsAsync();

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.Error("Error fetching return statistics", ex, correlationId);
            return StatusCode(500, "An error occurred while fetching return statistics");
        }
    }

    /// <summary>
    /// Delete a return (Admin only - use with caution)
    /// </summary>
    /// <route>DELETE /api/admin/returns/{id}</route>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteReturn(Guid id)
    {
        var correlationId = GetCorrelationId();
        var currentUserId = GetCurrentUserId();

        _logger.Warn($"Attempting to delete return: {id}", correlationId, new
        {
            returnId = id,
            deletedBy = currentUserId,
            endpoint = "DELETE /api/admin/returns/{id}"
        });

        try
        {
            var result = await _returnService.DeleteReturnAsync(id);

            if (!result)
            {
                _logger.Warn($"Return with ID {id} not found for deletion", correlationId, new
                {
                    returnId = id
                });
                return NotFound($"Return with ID {id} not found");
            }

            _logger.Info("RETURN_DELETED", correlationId, new
            {
                returnId = id,
                deletedBy = currentUserId,
                endpoint = "DELETE /api/admin/returns/{id}"
            });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.Error($"Error deleting return: {id}", ex, correlationId);
            return StatusCode(500, "An error occurred while deleting the return");
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
}
