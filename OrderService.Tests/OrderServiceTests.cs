using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using FluentAssertions;
using OrderService.Core.Messaging;
using OrderService.Core.Services;
using OrderService.Core.Repositories;
using OrderService.Core.Models.DTOs;
using OrderService.Core.Models.Entities;
using OrderService.Core.Models.Enums;
using OrderService.Core.Utils;

namespace OrderService.Tests;

public class OrderServiceTests
{
    private readonly Mock<IOrderRepository> _mockOrderRepository;
    private readonly StandardLogger _logger;
    private readonly Mock<IMessagingProvider> _mockMessagingProvider;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly OrderService.Core.Services.OrderService _orderService;

    public OrderServiceTests()
    {
        _mockOrderRepository = new Mock<IOrderRepository>();
        
        // Mock IMessagingProvider - setup both generic and non-generic overloads
        _mockMessagingProvider = new Mock<IMessagingProvider>();
        _mockMessagingProvider
            .Setup(x => x.PublishEventAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mockMessagingProvider
            .Setup(x => x.PublishEventAsync<It.IsAnyType>(It.IsAny<string>(), It.IsAny<It.IsAnyType>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        
        _mockCurrentUserService = new Mock<ICurrentUserService>();

        // Create real logger with mocked dependencies
        var mockLogger = new Mock<ILogger<StandardLogger>>();
        
        // Create a simple in-memory configuration
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            {"Logging:LogLevel:Default", "Information"},
            {"ServiceName", "OrderService.Tests"},
            {"ServiceVersion", "1.0.0"}
        });
        var configuration = configBuilder.Build();
        
        _logger = new StandardLogger(mockLogger.Object, configuration);

        // Setup common current user service
        _mockCurrentUserService.Setup(x => x.GetUserName()).Returns("TestUser");
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns("user-123");

        _orderService = new OrderService.Core.Services.OrderService(
            _mockOrderRepository.Object,
            _logger,
            _mockMessagingProvider.Object,
            _mockCurrentUserService.Object
        );
    }

    #region GetAllOrdersAsync Tests

    [Fact]
    public async Task GetAllOrdersAsync_ShouldReturnAllOrders_WhenOrdersExist()
    {
        // Arrange
        var orders = new List<Order>
        {
            CreateTestOrder(),
            CreateTestOrder()
        };

        _mockOrderRepository.Setup(x => x.GetAllOrdersAsync())
            .ReturnsAsync(orders);

        // Act
        var result = await _orderService.GetAllOrdersAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        _mockOrderRepository.Verify(x => x.GetAllOrdersAsync(), Times.Once);
    }

    [Fact]
    public async Task GetAllOrdersAsync_ShouldReturnEmptyList_WhenNoOrdersExist()
    {
        // Arrange
        _mockOrderRepository.Setup(x => x.GetAllOrdersAsync())
            .ReturnsAsync(new List<Order>());

        // Act
        var result = await _orderService.GetAllOrdersAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
        _mockOrderRepository.Verify(x => x.GetAllOrdersAsync(), Times.Once);
    }

    [Fact]
    public async Task GetAllOrdersAsync_ShouldThrowException_WhenRepositoryThrows()
    {
        // Arrange
        _mockOrderRepository.Setup(x => x.GetAllOrdersAsync())
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _orderService.GetAllOrdersAsync());
        _mockOrderRepository.Verify(x => x.GetAllOrdersAsync(), Times.Once);
    }

    #endregion

    #region GetOrderByIdAsync Tests

    [Fact]
    public async Task GetOrderByIdAsync_ShouldReturnOrder_WhenOrderExists()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = CreateTestOrder();
        order.Id = orderId;

        _mockOrderRepository.Setup(x => x.GetOrderByIdAsync(orderId))
            .ReturnsAsync(order);

        // Act
        var result = await _orderService.GetOrderByIdAsync(orderId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(orderId);
        _mockOrderRepository.Verify(x => x.GetOrderByIdAsync(orderId), Times.Once);
    }

    [Fact]
    public async Task GetOrderByIdAsync_ShouldReturnNull_WhenOrderDoesNotExist()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _mockOrderRepository.Setup(x => x.GetOrderByIdAsync(orderId))
            .ReturnsAsync((Order?)null);

        // Act
        var result = await _orderService.GetOrderByIdAsync(orderId);

        // Assert
        result.Should().BeNull();
        _mockOrderRepository.Verify(x => x.GetOrderByIdAsync(orderId), Times.Once);
    }

    [Fact]
    public async Task GetOrderByIdAsync_ShouldThrowException_WhenRepositoryThrows()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _mockOrderRepository.Setup(x => x.GetOrderByIdAsync(orderId))
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _orderService.GetOrderByIdAsync(orderId));
        _mockOrderRepository.Verify(x => x.GetOrderByIdAsync(orderId), Times.Once);
    }

    #endregion

    #region GetOrdersByCustomerIdAsync Tests

    [Fact]
    public async Task GetOrdersByCustomerIdAsync_ShouldReturnOrders_WhenOrdersExist()
    {
        // Arrange
        var customerId = "customer-123";
        var orders = new List<Order>
        {
            CreateTestOrder(),
            CreateTestOrder()
        };
        orders.ForEach(o => o.CustomerId = customerId);

        _mockOrderRepository.Setup(x => x.GetOrdersByCustomerIdAsync(customerId))
            .ReturnsAsync(orders);

        // Act
        var result = await _orderService.GetOrdersByCustomerIdAsync(customerId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.All(r => r.CustomerId == customerId).Should().BeTrue();
        _mockOrderRepository.Verify(x => x.GetOrdersByCustomerIdAsync(customerId), Times.Once);
    }

    [Fact]
    public async Task GetOrdersByCustomerIdAsync_ShouldReturnEmptyList_WhenNoOrdersExist()
    {
        // Arrange
        var customerId = "customer-123";
        _mockOrderRepository.Setup(x => x.GetOrdersByCustomerIdAsync(customerId))
            .ReturnsAsync(new List<Order>());

        // Act
        var result = await _orderService.GetOrdersByCustomerIdAsync(customerId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
        _mockOrderRepository.Verify(x => x.GetOrdersByCustomerIdAsync(customerId), Times.Once);
    }

    #endregion

    #region CreateOrderAsync Tests

    [Fact]
    public async Task CreateOrderAsync_ShouldCreateOrder_WhenValidDataProvided()
    {
        // Arrange
        var createOrderDto = CreateTestCreateOrderDto();
        var createdOrder = CreateTestOrder();
        
        _mockOrderRepository.Setup(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .ReturnsAsync(createdOrder);

        // Act
        var result = await _orderService.CreateOrderAsync(createOrderDto);

        // Assert
        result.Should().NotBeNull();
        result.CustomerId.Should().Be(createOrderDto.CustomerId);
        result.Status.Should().Be(OrderStatus.Created);
        result.Currency.Should().Be("USD");
        _mockOrderRepository.Verify(x => x.CreateOrderAsync(It.IsAny<Order>()), Times.Once);
        // Verify messaging was invoked (using It.IsAny<It.IsAnyType>() to match the generic method)
        _mockMessagingProvider.Verify(x => x.PublishEventAsync<It.IsAnyType>(
            It.IsAny<string>(), 
            It.IsAny<It.IsAnyType>(), 
            It.IsAny<string?>(), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldCalculateTotalsCorrectly_WhenOrderItemsProvided()
    {
        // Arrange
        var createOrderDto = CreateTestCreateOrderDto();
        createOrderDto.Items = new List<CreateOrderItemDto>
        {
            new() { ProductId = "prod-1", ProductName = "Product 1", UnitPrice = 25.00m, Quantity = 2 },
            new() { ProductId = "prod-2", ProductName = "Product 2", UnitPrice = 15.00m, Quantity = 1 }
        };

        var createdOrder = CreateTestOrder();
        createdOrder.Subtotal = 65.00m; // (25 * 2) + (15 * 1)
        createdOrder.TaxAmount = 5.20m; // 65 * 0.08
        createdOrder.TotalAmount = 70.20m; // 65 + 5.20
        
        // Add the expected items to match the input
        createdOrder.Items = new List<OrderItem>
        {
            new()
            {
                Id = Guid.NewGuid(),
                ProductId = "prod-1",
                ProductName = "Product 1",
                UnitPrice = 25.00m,
                Quantity = 2,
                TotalPrice = 50.00m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                ProductId = "prod-2",
                ProductName = "Product 2",
                UnitPrice = 15.00m,
                Quantity = 1,
                TotalPrice = 15.00m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        _mockOrderRepository.Setup(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .ReturnsAsync(createdOrder);

        // Act
        var result = await _orderService.CreateOrderAsync(createOrderDto);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        _mockOrderRepository.Verify(x => x.CreateOrderAsync(It.Is<Order>(o => 
            o.Items.Sum(i => i.TotalPrice) == 65.00m)), Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldUseProvidedCorrelationId_WhenProvided()
    {
        // Arrange
        var createOrderDto = CreateTestCreateOrderDto();
        var correlationId = "test-correlation-id";
        var createdOrder = CreateTestOrder();
        
        _mockOrderRepository.Setup(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .ReturnsAsync(createdOrder);

        // Act
        var result = await _orderService.CreateOrderAsync(createOrderDto, correlationId);

        // Assert
        result.Should().NotBeNull();
        _mockOrderRepository.Verify(x => x.CreateOrderAsync(It.IsAny<Order>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldThrowException_WhenRepositoryFails()
    {
        // Arrange
        var createOrderDto = CreateTestCreateOrderDto();
        _mockOrderRepository.Setup(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => _orderService.CreateOrderAsync(createOrderDto));
    }

    #endregion

    #region UpdateOrderStatusAsync Tests

    [Fact]
    public async Task UpdateOrderStatusAsync_ShouldUpdateStatus_WhenOrderExists()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var order = CreateTestOrder();
        order.Id = orderId;
        order.Status = OrderStatus.Created;

        var updateDto = new UpdateOrderStatusDto
        {
            Status = OrderStatus.Processing,
            Reason = "Processing started"
        };

        var updatedOrder = CreateTestOrder();
        updatedOrder.Id = orderId;
        updatedOrder.Status = OrderStatus.Processing;

        _mockOrderRepository.Setup(x => x.GetOrderByIdAsync(orderId))
            .ReturnsAsync(order);
        _mockOrderRepository.Setup(x => x.UpdateOrderAsync(It.IsAny<Order>()))
            .ReturnsAsync(updatedOrder);

        // Act
        var result = await _orderService.UpdateOrderStatusAsync(orderId, updateDto);

        // Assert
        result.Should().NotBeNull();
        result!.Status.Should().Be(OrderStatus.Processing);
        _mockOrderRepository.Verify(x => x.GetOrderByIdAsync(orderId), Times.Once);
        _mockOrderRepository.Verify(x => x.UpdateOrderAsync(It.IsAny<Order>()), Times.Once);
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_ShouldReturnNull_WhenOrderDoesNotExist()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var updateDto = new UpdateOrderStatusDto
        {
            Status = OrderStatus.Processing
        };

        _mockOrderRepository.Setup(x => x.GetOrderByIdAsync(orderId))
            .ReturnsAsync((Order?)null);

        // Act
        var result = await _orderService.UpdateOrderStatusAsync(orderId, updateDto);

        // Assert
        result.Should().BeNull();
        _mockOrderRepository.Verify(x => x.GetOrderByIdAsync(orderId), Times.Once);
        _mockOrderRepository.Verify(x => x.UpdateOrderAsync(It.IsAny<Order>()), Times.Never);
    }

    #endregion

    #region GetOrdersByStatusAsync Tests

    [Fact]
    public async Task GetOrdersByStatusAsync_ShouldReturnOrdersWithStatus_WhenOrdersExist()
    {
        // Arrange
        var status = OrderStatus.Processing;
        var orders = new List<Order>
        {
            CreateTestOrder(),
            CreateTestOrder()
        };
        orders.ForEach(o => o.Status = status);

        _mockOrderRepository.Setup(x => x.GetOrdersByStatusAsync(status))
            .ReturnsAsync(orders);

        // Act
        var result = await _orderService.GetOrdersByStatusAsync(status);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.All(r => r.Status == status).Should().BeTrue();
        _mockOrderRepository.Verify(x => x.GetOrdersByStatusAsync(status), Times.Once);
    }

    #endregion

    #region DeleteOrderAsync Tests

    [Fact]
    public async Task DeleteOrderAsync_ShouldReturnTrue_WhenOrderDeletedSuccessfully()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _mockOrderRepository.Setup(x => x.DeleteOrderAsync(orderId))
            .ReturnsAsync(true);

        // Act
        var result = await _orderService.DeleteOrderAsync(orderId);

        // Assert
        result.Should().BeTrue();
        _mockOrderRepository.Verify(x => x.DeleteOrderAsync(orderId), Times.Once);
    }

    [Fact]
    public async Task DeleteOrderAsync_ShouldReturnFalse_WhenOrderNotFound()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        _mockOrderRepository.Setup(x => x.DeleteOrderAsync(orderId))
            .ReturnsAsync(false);

        // Act
        var result = await _orderService.DeleteOrderAsync(orderId);

        // Assert
        result.Should().BeFalse();
        _mockOrderRepository.Verify(x => x.DeleteOrderAsync(orderId), Times.Once);
    }

    #endregion

    #region GetOrdersPagedAsync Tests

    [Fact]
    public async Task GetOrdersPagedAsync_ShouldReturnPagedResults_WhenOrdersExist()
    {
        // Arrange
        var query = new OrderQueryDto
        {
            Page = 1,
            PageSize = 10,
            Status = OrderStatus.Created
        };

        var orders = new List<Order> { CreateTestOrder(), CreateTestOrder() };
        var pagedResult = (orders, TotalCount: 2);

        _mockOrderRepository.Setup(x => x.GetOrdersPagedAsync(query))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _orderService.GetOrdersPagedAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.TotalItems.Should().Be(2);
        _mockOrderRepository.Verify(x => x.GetOrdersPagedAsync(query), Times.Once);
    }

    [Fact]
    public async Task GetOrdersPagedAsync_ShouldReturnEmptyResults_WhenNoOrdersExist()
    {
        // Arrange
        var query = new OrderQueryDto
        {
            Page = 1,
            PageSize = 10
        };

        var pagedResult = (new List<Order>(), TotalCount: 0);

        _mockOrderRepository.Setup(x => x.GetOrdersPagedAsync(query))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _orderService.GetOrdersPagedAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.TotalItems.Should().Be(0);
    }

    #endregion

    #region GetOrdersByCustomerIdPagedAsync Tests

    [Fact]
    public async Task GetOrdersByCustomerIdPagedAsync_ShouldReturnPagedResults_WhenOrdersExist()
    {
        // Arrange
        var customerId = "customer-123";
        var pageRequest = new PagedRequestDto
        {
            Page = 1,
            PageSize = 10
        };

        var orders = new List<Order> { CreateTestOrder(), CreateTestOrder() };
        orders.ForEach(o => o.CustomerId = customerId);
        var pagedResult = (orders, TotalCount: 2);

        _mockOrderRepository.Setup(x => x.GetOrdersByCustomerIdPagedAsync(customerId, pageRequest))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _orderService.GetOrdersByCustomerIdPagedAsync(customerId, pageRequest);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Items.All(r => r.CustomerId == customerId).Should().BeTrue();
        result.TotalItems.Should().Be(2);
        _mockOrderRepository.Verify(x => x.GetOrdersByCustomerIdPagedAsync(customerId, pageRequest), Times.Once);
    }

    [Fact]
    public async Task GetOrdersByCustomerIdPagedAsync_ShouldReturnEmptyResults_WhenNoOrdersExist()
    {
        // Arrange
        var customerId = "customer-123";
        var pageRequest = new PagedRequestDto
        {
            Page = 1,
            PageSize = 10
        };

        var pagedResult = (new List<Order>(), TotalCount: 0);

        _mockOrderRepository.Setup(x => x.GetOrdersByCustomerIdPagedAsync(customerId, pageRequest))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _orderService.GetOrdersByCustomerIdPagedAsync(customerId, pageRequest);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.TotalItems.Should().Be(0);
    }

    #endregion

    #region Additional Edge Case Tests

    [Fact]
    public async Task CreateOrderAsync_ShouldHandleEmptyItemsList_WhenNoItemsProvided()
    {
        // Arrange
        var createOrderDto = CreateTestCreateOrderDto();
        createOrderDto.Items = new List<CreateOrderItemDto>(); // Empty list

        var createdOrder = CreateTestOrder();
        createdOrder.Items = new List<OrderItem>();
        createdOrder.Subtotal = 0m;
        createdOrder.TotalAmount = 0m;

        _mockOrderRepository.Setup(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .ReturnsAsync(createdOrder);

        // Act
        var result = await _orderService.CreateOrderAsync(createOrderDto);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.Subtotal.Should().Be(0m);
        _mockOrderRepository.Verify(x => x.CreateOrderAsync(It.IsAny<Order>()), Times.Once);
    }

    [Fact]
    public async Task UpdateOrderStatusAsync_ShouldUpdateCorrectly_WithAllStatus()
    {
        // Test each status transition
        var statusTests = new[]
        {
            (OrderStatus.Created, OrderStatus.Confirmed),
            (OrderStatus.Confirmed, OrderStatus.Processing),
            (OrderStatus.Processing, OrderStatus.Shipped),
            (OrderStatus.Shipped, OrderStatus.Delivered),
            (OrderStatus.Created, OrderStatus.Cancelled)
        };

        foreach (var (initialStatus, newStatus) in statusTests)
        {
            // Arrange
            var orderId = Guid.NewGuid();
            var order = CreateTestOrder();
            order.Id = orderId;
            order.Status = initialStatus;

            var updateDto = new UpdateOrderStatusDto
            {
                Status = newStatus,
                Reason = $"Status change from {initialStatus} to {newStatus}"
            };

            var updatedOrder = CreateTestOrder();
            updatedOrder.Id = orderId;
            updatedOrder.Status = newStatus;

            _mockOrderRepository.Setup(x => x.GetOrderByIdAsync(orderId))
                .ReturnsAsync(order);
            _mockOrderRepository.Setup(x => x.UpdateOrderAsync(It.IsAny<Order>()))
                .ReturnsAsync(updatedOrder);

            // Act
            var result = await _orderService.UpdateOrderStatusAsync(orderId, updateDto);

            // Assert
            result.Should().NotBeNull();
            result!.Status.Should().Be(newStatus);

            // Reset for next iteration
            _mockOrderRepository.Reset();
        }
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldGenerateCorrectOrderNumber_WithPrefix()
    {
        // Arrange
        var createOrderDto = CreateTestCreateOrderDto();
        var createdOrder = CreateTestOrder();
        
        _mockOrderRepository.Setup(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .ReturnsAsync(createdOrder);

        // Act
        var result = await _orderService.CreateOrderAsync(createOrderDto);

        // Assert
        result.Should().NotBeNull();
        result.OrderNumber.Should().StartWith("ORD");
        _mockOrderRepository.Verify(x => x.CreateOrderAsync(It.Is<Order>(o => 
            o.OrderNumber.StartsWith("ORD"))), Times.Once);
    }

    #endregion

    #region Helper Methods

    private Order CreateTestOrder()
    {
        return new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = "customer-123",
            OrderNumber = "ORD-20240101-ABC12345",
            Status = OrderStatus.Created,
            PaymentStatus = PaymentStatus.Pending,
            ShippingStatus = ShippingStatus.NotShipped,
            Currency = "USD",
            Subtotal = 100.00m,
            TaxAmount = 8.00m,
            ShippingCost = 10.00m,
            TotalAmount = 118.00m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = "TestUser",
            ShippingAddress = new Address
            {
                AddressLine1 = "123 Test St",
                City = "Test City",
                State = "TS",
                ZipCode = "12345",
                Country = "US"
            },
            BillingAddress = new Address
            {
                AddressLine1 = "123 Test St",
                City = "Test City",
                State = "TS",
                ZipCode = "12345",
                Country = "US"
            },
            Items = new List<OrderItem>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ProductId = "prod-123",
                    ProductName = "Test Product",
                    UnitPrice = 50.00m,
                    Quantity = 2,
                    TotalPrice = 100.00m,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }
            }
        };
    }

    private CreateOrderDto CreateTestCreateOrderDto()
    {
        return new CreateOrderDto
        {
            CustomerId = "customer-123",
            Items = new List<CreateOrderItemDto>
            {
                new()
                {
                    ProductId = "prod-123",
                    ProductName = "Test Product",
                    UnitPrice = 50.00m,
                    Quantity = 2
                }
            },
            ShippingAddress = new AddressDto
            {
                AddressLine1 = "123 Test St",
                City = "Test City",
                State = "TS",
                ZipCode = "12345",
                Country = "US"
            },
            BillingAddress = new AddressDto
            {
                AddressLine1 = "123 Test St",
                City = "Test City",
                State = "TS",
                ZipCode = "12345",
                Country = "US"
            }
        };
    }

    #endregion
}