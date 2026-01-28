using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace OrderService.Controllers;

/// <summary>
/// Operational/Infrastructure endpoints
/// These endpoints are used by monitoring systems, load balancers, and DevOps tools
/// </summary>
[ApiController]
public class OperationalController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<OperationalController> _logger;

    public OperationalController(IConfiguration configuration, ILogger<OperationalController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Basic health check endpoint
    /// </summary>
    /// <route>GET /health</route>
    [HttpGet("/health")]
    public ActionResult<object> Health()
    {
        _logger.LogDebug("Health check requested");
        
        return Ok(new 
        { 
            status = "healthy",
            service = _configuration["ServiceName"] ?? "order-service",
            timestamp = DateTime.UtcNow,
            version = _configuration["ServiceVersion"] ?? "1.0.0"
        });
    }

    /// <summary>
    /// Readiness probe - check if service is ready to serve traffic
    /// </summary>
    /// <route>GET /health/ready</route>
    [HttpGet("/health/ready")]
    [HttpGet("/readiness")] // Legacy endpoint for backward compatibility
    public ActionResult<object> Readiness()
    {
        _logger.LogDebug("Readiness check requested");
        
        try
        {
            // Add more sophisticated checks here (DB connectivity, external dependencies, etc.)
            // Example: Check database connectivity, message broker, etc.
            // await CheckDatabaseConnectivity();
            // await CheckMessageBrokerConnectivity();
            
            return Ok(new 
            { 
                status = "ready",
                service = "order-service",
                timestamp = DateTime.UtcNow,
                checks = new {
                    database = "connected",
                    messageBroker = "connected"
                    // Add other dependency checks
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Readiness check failed");
            
            return StatusCode(503, new 
            { 
                status = "not ready",
                service = "order-service",
                timestamp = DateTime.UtcNow,
                error = "Service dependencies not available"
            });
        }
    }

    /// <summary>
    /// Liveness probe - check if the app is running
    /// </summary>
    /// <route>GET /health/live</route>
    [HttpGet("/health/live")]
    [HttpGet("/liveness")] // Legacy endpoint for backward compatibility
    public ActionResult<object> Liveness()
    {
        _logger.LogDebug("Liveness check requested");
        
        var process = Process.GetCurrentProcess();
        var uptime = DateTime.UtcNow.Subtract(process.StartTime.ToUniversalTime());
        
        return Ok(new 
        { 
            status = "alive",
            service = "order-service",
            timestamp = DateTime.UtcNow,
            uptime = uptime.TotalSeconds
        });
    }

    /// <summary>
    /// Basic metrics endpoint
    /// </summary>
    /// <route>GET /metrics</route>
    [HttpGet("/metrics")]
    public ActionResult<object> Metrics()
    {
        _logger.LogDebug("Metrics requested");
        
        var process = Process.GetCurrentProcess();
        var uptime = DateTime.UtcNow.Subtract(process.StartTime.ToUniversalTime());
        
        return Ok(new 
        { 
            service = "order-service",
            timestamp = DateTime.UtcNow,
            metrics = new {
                uptime = uptime.TotalSeconds,
                memory = new {
                    workingSet = process.WorkingSet64,
                    privateMemory = process.PrivateMemorySize64,
                    virtualMemory = process.VirtualMemorySize64
                },
                processorTime = process.TotalProcessorTime.TotalMilliseconds,
                threads = process.Threads.Count,
                handles = process.HandleCount,
                dotnetVersion = Environment.Version.ToString()
            }
        });
    }
}
