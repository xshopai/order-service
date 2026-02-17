using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace OrderService.Core.Services;

/// <summary>
/// Service for retrieving secrets/configuration from environment variables and configuration files
/// </summary>
public class ConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly IConfiguration _configuration;

    public ConfigurationService(ILogger<ConfigurationService> logger, IConfiguration configuration)
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
    /// Get JWT configuration from environment variables or hierarchical config
    /// </summary>
    public Task<(string Secret, string Issuer, string Audience)> GetJwtConfigAsync(CancellationToken cancellationToken = default)
    {
        // Check flat key first (env var style), then hierarchical (.NET standard)
        var secret = _configuration["JWT_SECRET"] ?? _configuration["Jwt:Secret"];
        if (string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException("JWT secret not found. Set JWT_SECRET or Jwt:Secret in configuration.");
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
