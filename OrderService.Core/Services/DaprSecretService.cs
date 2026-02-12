using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace OrderService.Core.Services;

/// <summary>
/// Service for retrieving secrets from environment variables/configuration
/// </summary>
public class DaprSecretService
{
    private readonly ILogger<DaprSecretService> _logger;
    private readonly IConfiguration _configuration;

    public DaprSecretService(ILogger<DaprSecretService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Get a secret value from environment variables/configuration
    /// </summary>
    /// <param name="secretName">Name of the secret (e.g., "JWT_SECRET")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Secret value</returns>
    /// <exception cref="InvalidOperationException">Thrown when secret is not found</exception>
    public Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        // Check environment variables / configuration
        var configValue = _configuration[secretName];
        if (!string.IsNullOrEmpty(configValue))
        {
            _logger.LogDebug("Retrieved secret '{SecretName}' from configuration/env var", secretName);
            return Task.FromResult(configValue);
        }

        var errorMessage = $"Secret '{secretName}' not found in configuration/environment variables";
        _logger.LogError(errorMessage);
        throw new InvalidOperationException(errorMessage);
    }

    /// <summary>
    /// Get JWT configuration from environment variables
    /// </summary>
    public Task<(string Secret, string Issuer, string Audience)> GetJwtConfigAsync(CancellationToken cancellationToken = default)
    {
        var secret = _configuration["JWT_SECRET"];
        if (string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException("JWT_SECRET not found in configuration/environment variables");
        }
        
        var issuer = _configuration["Jwt:Issuer"] ?? "auth-service";
        var audience = _configuration["Jwt:Audience"] ?? "xshopai-platform";

        return Task.FromResult((secret, issuer, audience));
    }

    /// <summary>
    /// Get database connection string from environment variables
    /// </summary>
    public Task<string> GetDatabaseConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        return GetSecretAsync("DATABASE_CONNECTION_STRING", cancellationToken);
    }
}
