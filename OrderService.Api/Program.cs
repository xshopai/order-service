using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using FluentValidation;
using FluentValidation.AspNetCore;
using OrderService.Core.Data;
using OrderService.Core.Messaging;
using OrderService.Core.Repositories;
using OrderService.Core.Services;
using OrderService.Core.Extensions;
using OrderService.Core.Validators;
using OrderService.Core.Utils;
using Serilog;
using Serilog.Events;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Azure.Monitor.OpenTelemetry.Exporter;

// Configure Serilog with colored console output
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:yyyy-MM-dd'T'HH:mm:ss.fff'Z'} [{Level:u3}]: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Use Serilog for logging
builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers()
    .AddDapr() // Add Dapr integration
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderDtoValidator>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your valid token in the text input below.\n\nExample: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\""
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// Add JWT Authentication and Authorization (from OrderService.Core)
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddOrderServiceAuthorization();

// Add Entity Framework with SQL Server - Lazy load connection string from Dapr secrets
builder.Services.AddDbContext<OrderDbContext>((serviceProvider, options) =>
{
    var secretService = serviceProvider.GetRequiredService<DaprSecretService>();
    var connectionString = secretService.GetDatabaseConnectionStringAsync().GetAwaiter().GetResult();
    options.UseSqlServer(
        connectionString,
        b => b.MigrationsAssembly("OrderService.Api"));
});

// Register repositories and services
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService.Core.Services.OrderService>();
builder.Services.AddScoped<IOrderReturnRepository, OrderReturnRepository>();
builder.Services.AddScoped<IOrderReturnService, OrderReturnService>();

// Register current user service for JWT authentication
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Register StandardLogger
builder.Services.AddSingleton<StandardLogger>();

// Register Dapr services
builder.Services.AddSingleton<DaprSecretService>();

// Register Messaging abstraction layer (supports dapr, rabbitmq, servicebus via MESSAGING_PROVIDER config)
builder.Services.AddMessaging(builder.Configuration);

// Configure OpenTelemetry tracing based on OTEL_TRACES_EXPORTER environment variable
// Supported values: zipkin, otlp, azure, none (default)
var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "order-service";
var tracesExporter = Environment.GetEnvironmentVariable("OTEL_TRACES_EXPORTER")?.ToLower() ?? "none";

switch (tracesExporter)
{
    case "zipkin":
        var zipkinEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_ZIPKIN_ENDPOINT") ?? "http://localhost:9411/api/v2/spans";
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddZipkinExporter(options => options.Endpoint = new Uri(zipkinEndpoint)));
        Log.Information("✅ Tracing: Zipkin exporter → {Endpoint}", zipkinEndpoint);
        break;

    case "otlp":
        var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4318";
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint)));
        Log.Information("✅ Tracing: OTLP exporter → {Endpoint}", otlpEndpoint);
        break;

    case "azure":
        var connectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
        if (!string.IsNullOrEmpty(connectionString))
        {
            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(serviceName))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddAzureMonitorTraceExporter(options => options.ConnectionString = connectionString));
            Log.Information("✅ Tracing: Azure Monitor configured for {ServiceName}", serviceName);
        }
        else
        {
            Log.Warning("⚠️  Azure exporter selected but APPLICATIONINSIGHTS_CONNECTION_STRING not set");
        }
        break;

    case "none":
    default:
        Log.Information("ℹ️  Tracing disabled (OTEL_TRACES_EXPORTER={Exporter})", tracesExporter);
        break;
}

var app = builder.Build();

// Apply database migrations at startup with retry logic
// Wait for Dapr sidecar and database connectivity
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var maxRetries = 5;
    var retryDelaySeconds = 5;
    
    for (int retry = 0; retry < maxRetries; retry++)
    {
        try
        {
            if (retry > 0)
            {
                Log.Information("Waiting {Delay} seconds before retry {Retry}/{MaxRetries}...", 
                    retryDelaySeconds, retry + 1, maxRetries);
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
            }
            
            var context = services.GetRequiredService<OrderDbContext>();
            Log.Information("Applying database migrations (attempt {Attempt}/{MaxRetries})...", 
                retry + 1, maxRetries);
            await context.Database.MigrateAsync();
            Log.Information("Database migrations applied successfully");
            break; // Success - exit retry loop
        }
        catch (Exception ex) when (retry < maxRetries - 1)
        {
            Log.Warning(ex, "Migration attempt {Attempt} failed, will retry...", retry + 1);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "All migration attempts failed. Service will start but database may not be ready.");
            // Don't throw - allow app to start even if migrations fail
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Add W3C Trace Context middleware
app.UseTraceContext();

// Add Authentication and Authorization middleware
app.UseOrderServiceAuthentication();

// Enable Dapr CloudEvents for publishing
app.UseCloudEvents();

app.MapControllers();

try
{
    Log.Information("Starting Order Service API with Dapr integration");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Order Service terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
