using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OrderService.Core.Configuration;
using OrderService.Core.Services;
using System.Text;

namespace OrderService.Core.Extensions;

/// <summary>
/// Extension methods for configuring JWT authentication and authorization
/// </summary>
public static class JwtAuthenticationExtensions
{
    /// <summary>
    /// Adds JWT authentication to the service collection with lazy loading from Dapr secrets
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add JWT Authentication with lazy loading of secrets
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Use lazy loading pattern - load JWT secret on first request
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogInformation("JWT OnMessageReceived: Token={Token}", 
                            context.Request.Headers["Authorization"].ToString());
                        
                        // Only load JWT configuration once (check if already loaded)
                        if (options.TokenValidationParameters?.IssuerSigningKey == null)
                        {
                            try
                            {
                                var secretService = context.HttpContext.RequestServices.GetRequiredService<DaprSecretService>();
                                var (secret, issuer, audience) = secretService.GetJwtConfigAsync().GetAwaiter().GetResult();

                                if (string.IsNullOrEmpty(secret))
                                {
                                    throw new InvalidOperationException("JWT secret not found in Dapr secrets");
                                }

                                options.TokenValidationParameters = new TokenValidationParameters
                                {
                                    ValidateIssuer = true,            // Validate issuer (auth-service)
                                    ValidIssuer = issuer,             // Expected issuer
                                    ValidateAudience = true,          // Validate audience (xshopai-platform)
                                    ValidAudience = audience,         // Expected audience
                                    ValidateLifetime = true,          // Validate token expiration
                                    ValidateIssuerSigningKey = true,  // Validate signature with secret key
                                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
                                };
                            }
                            catch (Exception ex)
                            {
                                // Check if Dapr secret store is not ready (check all levels of inner exceptions)
                                var currentEx = ex;
                                bool isDaprNotReady = false;
                                while (currentEx != null)
                                {
                                    if (currentEx.Message?.Contains("secret store is not configured") == true)
                                    {
                                        isDaprNotReady = true;
                                        break;
                                    }
                                    currentEx = currentEx.InnerException;
                                }

                                if (isDaprNotReady)
                                {
                                    // Dapr not ready yet - silently skip authentication for this request
                                    context.HttpContext.RequestServices
                                        .GetRequiredService<ILogger<JwtBearerEvents>>()
                                        .LogWarning("Dapr secret store not ready yet, skipping JWT authentication for this request");
                                    context.NoResult();
                                    return Task.CompletedTask;
                                }

                                // Other errors should still fail
                                context.HttpContext.RequestServices
                                    .GetRequiredService<ILogger<JwtBearerEvents>>()
                                    .LogError(ex, "Failed to load JWT configuration from Dapr secrets");
                                throw;
                            }
                        }
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogInformation("JWT Token validated successfully. User: {User}, Claims: {Claims}",
                            context.Principal?.Identity?.Name,
                            string.Join(", ", context.Principal?.Claims?.Select(c => $"{c.Type}={c.Value}") ?? new List<string>()));
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogError(context.Exception, "JWT Authentication failed");
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<JwtBearerEvents>>();
                        logger.LogWarning("JWT Challenge: Error={Error}, ErrorDescription={ErrorDescription}",
                            context.Error, context.ErrorDescription);
                        return Task.CompletedTask;
                    }
                };
            });

        return services;
    }

    /// <summary>
    /// Adds role-based authorization policies for Order Service
    /// </summary>
    public static IServiceCollection AddOrderServiceAuthorization(
        this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy("CustomerOnly", policy => 
                policy.RequireRole("customer"));
            
            options.AddPolicy("AdminOnly", policy => 
                policy.RequireRole("admin"));
            
            // User can be EITHER customer OR admin (not both at same time)
            options.AddPolicy("CustomerOrAdmin", policy => 
                policy.RequireRole("customer", "admin"));
        });

        return services;
    }

    /// <summary>
    /// Adds authentication and authorization middleware to the pipeline
    /// Must be called after UseCorrelationId and before MapControllers
    /// </summary>
    public static IApplicationBuilder UseOrderServiceAuthentication(this IApplicationBuilder app)
    {
        // Add Authentication middleware (validates JWT tokens)
        app.UseAuthentication();
        
        // Add Authorization middleware (enforces policies)
        app.UseAuthorization();
        
        return app;
    }
}
