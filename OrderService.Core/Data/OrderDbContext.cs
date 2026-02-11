using Microsoft.EntityFrameworkCore;
using OrderService.Core.Models.Entities;

namespace OrderService.Core.Data
{
    public class OrderDbContext : DbContext
    {
        public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
        {
        }

        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;
        public DbSet<OrderReturn> OrderReturns { get; set; } = null!;
        public DbSet<ReturnItem> ReturnItems { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Order entity
            modelBuilder.Entity<Order>(entity =>
            {
                entity.ToTable("Orders");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.CustomerId).IsRequired();
                entity.Property(e => e.Currency).HasMaxLength(3);
                entity.Property(e => e.CreatedBy).IsRequired();
                
                // Configure decimal properties with precision for SQL Server
                entity.Property(e => e.Subtotal).HasPrecision(18, 2);
                entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
                entity.Property(e => e.TaxRate).HasPrecision(18, 4);
                entity.Property(e => e.ShippingCost).HasPrecision(18, 2);
                entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
                entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
                
                // Add indexes for commonly queried fields
                entity.HasIndex(e => e.OrderNumber).IsUnique();
                entity.HasIndex(e => e.CustomerId);
                entity.HasIndex(e => e.CreatedAt);
                
                // Configure owned entity types for addresses
                entity.OwnsOne(e => e.ShippingAddress, sa =>
                {
                    sa.Property(a => a.AddressLine1).HasColumnName("ShippingAddressLine1").HasMaxLength(100);
                    sa.Property(a => a.AddressLine2).HasColumnName("ShippingAddressLine2").HasMaxLength(100);
                    sa.Property(a => a.City).HasColumnName("ShippingCity").HasMaxLength(50);
                    sa.Property(a => a.State).HasColumnName("ShippingState").HasMaxLength(50);
                    sa.Property(a => a.ZipCode).HasColumnName("ShippingZipCode").HasMaxLength(20);
                    sa.Property(a => a.Country).HasColumnName("ShippingCountry").HasMaxLength(2).HasDefaultValue("US");
                });
                
                entity.OwnsOne(e => e.BillingAddress, ba =>
                {
                    ba.Property(a => a.AddressLine1).HasColumnName("BillingAddressLine1").HasMaxLength(100);
                    ba.Property(a => a.AddressLine2).HasColumnName("BillingAddressLine2").HasMaxLength(100);
                    ba.Property(a => a.City).HasColumnName("BillingCity").HasMaxLength(50);
                    ba.Property(a => a.State).HasColumnName("BillingState").HasMaxLength(50);
                    ba.Property(a => a.ZipCode).HasColumnName("BillingZipCode").HasMaxLength(20);
                    ba.Property(a => a.Country).HasColumnName("BillingCountry").HasMaxLength(2).HasDefaultValue("US");
                });
            });

            // Configure OrderItem entity
            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.ToTable("OrderItems");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ProductName).IsRequired();
                entity.Property(e => e.Quantity).IsRequired();
                
                // Configure decimal properties with precision for SQL Server
                entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
                entity.Property(e => e.OriginalPrice).HasPrecision(18, 2);
                entity.Property(e => e.TotalPrice).HasPrecision(18, 2);
                entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
                entity.Property(e => e.DiscountPercentage).HasPrecision(18, 4);
                entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
                entity.Property(e => e.ShippingCostPerItem).HasPrecision(18, 2);
                entity.Property(e => e.GiftWrapCost).HasPrecision(18, 2);
                entity.Property(e => e.RefundedAmount).HasPrecision(18, 2);
                
                // Add indexes for commonly queried fields
                entity.HasIndex(e => e.OrderId);
                entity.HasIndex(e => e.ProductId);
                
                // Configure relationship
                entity.HasOne(e => e.Order)
                      .WithMany(o => o.Items)
                      .HasForeignKey(e => e.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure OrderReturn entity
            modelBuilder.Entity<OrderReturn>(entity =>
            {
                entity.ToTable("OrderReturns");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ReturnNumber).IsRequired().HasMaxLength(50);
                entity.Property(e => e.OrderId).IsRequired();
                entity.Property(e => e.CustomerId).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.Reason).IsRequired();
                entity.Property(e => e.Description).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.Currency).HasMaxLength(3);
                
                // Configure decimal properties with precision
                entity.Property(e => e.RefundAmount).HasPrecision(18, 2);
                entity.Property(e => e.ShippingRefund).HasPrecision(18, 2);
                entity.Property(e => e.TotalRefund).HasPrecision(18, 2);
                
                // Optional fields
                entity.Property(e => e.RejectionReason).HasMaxLength(500);
                entity.Property(e => e.InspectionNotes).HasMaxLength(1000);
                entity.Property(e => e.ReturnShippingCarrier).HasMaxLength(100);
                entity.Property(e => e.ReturnTrackingNumber).HasMaxLength(100);
                entity.Property(e => e.ApprovedBy).HasMaxLength(100);
                entity.Property(e => e.ProcessedBy).HasMaxLength(100);
                
                // Add indexes for commonly queried fields
                entity.HasIndex(e => e.ReturnNumber).IsUnique();
                entity.HasIndex(e => e.OrderId);
                entity.HasIndex(e => e.CustomerId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CreatedAt);
                
                // Configure relationship with Order
                entity.HasOne(e => e.Order)
                      .WithMany()
                      .HasForeignKey(e => e.OrderId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure ReturnItem entity
            modelBuilder.Entity<ReturnItem>(entity =>
            {
                entity.ToTable("ReturnItems");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OrderReturnId).IsRequired();
                entity.Property(e => e.OrderItemId).IsRequired();
                entity.Property(e => e.ProductId).IsRequired();
                entity.Property(e => e.ProductName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.QuantityToReturn).IsRequired();
                
                // Configure decimal properties with precision
                entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
                entity.Property(e => e.RefundAmount).HasPrecision(18, 2);
                
                // Optional fields
                entity.Property(e => e.ProductImageUrl).HasMaxLength(500);
                entity.Property(e => e.ItemCondition).HasMaxLength(500);
                
                // Add indexes
                entity.HasIndex(e => e.OrderReturnId);
                entity.HasIndex(e => e.OrderItemId);
                entity.HasIndex(e => e.ProductId);
                
                // Configure relationship with OrderReturn
                entity.HasOne<OrderReturn>()
                      .WithMany(r => r.Items)
                      .HasForeignKey(e => e.OrderReturnId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
