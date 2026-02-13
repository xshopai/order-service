using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace OrderService.Core.Messaging;

/// <summary>
/// RabbitMQ-based messaging provider implementation.
/// Directly connects to RabbitMQ without Dapr abstraction.
/// </summary>
public class RabbitMQMessagingProvider : IMessagingProvider
{
    private readonly ILogger<RabbitMQMessagingProvider> _logger;
    private readonly string _connectionString;
    private readonly string _exchangeName;
    private bool _disposed;

    // RabbitMQ connection objects (lazy initialized)
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    public string ProviderName => "rabbitmq";

    public RabbitMQMessagingProvider(
        ILogger<RabbitMQMessagingProvider> logger,
        string connectionString,
        string exchangeName = "xshopai.events")
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _exchangeName = exchangeName;
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel != null && _channel.IsOpen)
            return _channel;

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_channel != null && _channel.IsOpen)
                return _channel;

            var factory = new ConnectionFactory
            {
                Uri = new Uri(_connectionString)
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            // Declare the exchange (topic type for routing key based routing)
            await _channel.ExchangeDeclareAsync(
                exchange: _exchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "RabbitMQ connection established: Exchange={Exchange}",
                _exchangeName);

            return _channel;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<bool> PublishEventAsync(
        string topic,
        object eventData,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        return await PublishEventInternalAsync(topic, eventData, correlationId, cancellationToken);
    }

    public async Task<bool> PublishEventAsync<T>(
        string topic,
        T eventData,
        string? correlationId = null,
        CancellationToken cancellationToken = default) where T : class
    {
        return await PublishEventInternalAsync(topic, eventData, correlationId, cancellationToken);
    }

    private async Task<bool> PublishEventInternalAsync(
        string topic,
        object eventData,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Publishing event via RabbitMQ: Exchange={Exchange}, Topic={Topic}, CorrelationId={CorrelationId}",
                _exchangeName,
                topic,
                correlationId ?? "N/A");

            var channel = await GetChannelAsync(cancellationToken);

            // Serialize the message
            var messageBody = JsonSerializer.SerializeToUtf8Bytes(eventData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            // Set message properties
            var properties = new BasicProperties
            {
                Persistent = true,
                CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
                ContentType = "application/json",
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                AppId = "order-service"
            };

            // Publish to exchange with topic as routing key
            await channel.BasicPublishAsync(
                exchange: _exchangeName,
                routingKey: topic,
                mandatory: false,
                basicProperties: properties,
                body: messageBody,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Successfully published event via RabbitMQ: Topic={Topic}, CorrelationId={CorrelationId}, Size={Size} bytes",
                topic,
                correlationId ?? "N/A",
                messageBody.Length);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish event via RabbitMQ: Topic={Topic}, CorrelationId={CorrelationId}",
                topic,
                correlationId ?? "N/A");
            return false;
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            return channel.IsOpen;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RabbitMQ health check failed");
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        try
        {
            if (_channel != null)
            {
                await _channel.CloseAsync();
                _channel.Dispose();
            }
            if (_connection != null)
            {
                await _connection.CloseAsync();
                _connection.Dispose();
            }
            
            _logger.LogInformation("RabbitMQ messaging provider disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing RabbitMQ messaging provider");
        }

        _disposed = true;
    }
}
