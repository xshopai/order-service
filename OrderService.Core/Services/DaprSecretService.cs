using Dapr.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace OrderService.Core.Services;

/// <summary>
/// Service for retrieving secrets from Dapr Secret Store
/// </summary>
public class DaprSecretService
{
    private readonly DaprClient _daprClient;
    private readonly ILogger<DaprSecretService> _logger;
    private readonly IConfiguration _configuration;
    private const string SecretStoreName = "secretstore";

    public DaprSecretService(DaprClient daprClient, ILogger<DaprSecretService> logger, IConfiguration configuration)
    {
        _daprClient = daprClient;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Get a secret value - first checks environment/config, then Dapr Secret Store
    /// In Azure Container Apps, secrets are injected as env vars at deployment time.
    /// Dapr secretstore is only used locally with .dapr/secrets.json
    /// </summary>
    /// <param name="secretName">Name of the secret (e.g., "JWT_SECRET")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Secret value</returns>
    /// <exception cref="InvalidOperationException">Thrown when secret is not found</exception>
    public async Task<string> GetSecretAsync(string secretName, CancellationToken cancellationToken = default)
    {
        // First, check environment variables / configuration
        // In Azure Container Apps, secrets are injected as env vars at deployment time
        var configValue = _configuration[secretName];
        if (!string.IsNullOrEmpty(configValue))
        {
            _logger.LogDebug("Retrieved secret '{SecretName}' from configuration/env var", secretName);
            return configValue;
        }

        // Fallback to Dapr Secret Store (used locally with .dapr/secrets.json)
        try
        {
            _logger.LogDebug("Retrieving secret: {SecretName} from Dapr store: {StoreName}", secretName, SecretStoreName);

            var secrets = await _daprClient.GetSecretAsync(
                SecretStoreName,
                secretName,
                cancellationToken: cancellationToken);

            if (secrets != null && secrets.Count > 0)
            {
                var value = secrets.FirstOrDefault().Value;
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve secret '{SecretName}' from Dapr secret store", secretName);
        }

        var errorMessage = $"Secret '{secretName}' not found in configuration or Dapr secret store";
        _logger.LogError(errorMessage);
        throw new InvalidOperationException(errorMessage);
    }

    /// <summary>
    /// Get JWT configuration from secrets
    /// </summary>
    public async Task<(string Secret, string Issuer, string Audience)> GetJwtConfigAsync(CancellationToken cancellationToken = default)
    {
        var secret = await GetSecretAsync("JWT_SECRET", cancellationToken);
        // Issuer and Audience are typically fixed values, not secrets
        var issuer = _configuration["Jwt:Issuer"] ?? "auth-service";
        var audience = _configuration["Jwt:Audience"] ?? "xshopai-platform";

        return (secret, issuer, audience);
    }

    /// <summary>
    /// Get database connection string from secrets
    /// </summary>
    public async Task<string> GetDatabaseConnectionStringAsync(CancellationToken cancellationToken = default)
    {
        return await GetSecretAsync("DATABASE_CONNECTION_STRING", cancellationToken);
    }
}
