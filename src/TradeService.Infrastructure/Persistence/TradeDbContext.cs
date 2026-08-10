using Microsoft.EntityFrameworkCore;
using TradeService.Domain.Entities;

namespace TradeService.Infrastructure.Persistence;

public class TradeDbContext(DbContextOptions<TradeDbContext> options) : DbContext(options)
{
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<Price> Prices => Set<Price>();
    public DbSet<FxRate> FxRates => Set<FxRate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Trade>(builder =>
        {
            builder.ToTable("Trades", "dbo");
            builder.HasKey(t => t.Id);

            builder.Property(t => t.ExternalRef)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(t => t.AccountId)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(t => t.Isin)
                .HasColumnType("char(12)")
                .IsRequired();

            builder.Property(t => t.Symbol)
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(t => t.Side)
                .HasConversion<string>()
                .HasMaxLength(10)
                .IsRequired();

            builder.Property(t => t.Quantity)
                .HasPrecision(18, 4)
                .IsRequired();

            builder.Property(t => t.Price)
                .HasPrecision(18, 4)
                .IsRequired();

            builder.Property(t => t.TradeDate)
                .HasColumnType("date")
                .IsRequired();

            builder.Property(t => t.AsOf)
                .IsRequired();

            builder.Property(t => t.CreatedAtUtc)
                .IsRequired();
            
            builder.HasIndex(t => new { t.ExternalRef, t.AsOf }, "IX_Trades_ExternalRef_AsOf");

            builder.HasIndex(t => new { t.AccountId, t.TradeDate }, "IX_Trades_AccountId_TradeDate");
        });

        modelBuilder.Entity<Price>(builder =>
        {
            builder.ToTable("Prices", "dbo");
            builder.HasKey(p => p.Id);

            builder.Property(p => p.Isin)
                .HasColumnType("char(12)")
                .IsRequired();

            builder.Property(p => p.PriceDate)
                .HasColumnType("date")
                .IsRequired();

            builder.Property(p => p.PriceValue)
                .HasColumnName("Price")
                .HasPrecision(18, 4)
                .IsRequired();

            builder.Property(p => p.Currency)
                .HasColumnType("char(3)")
                .HasDefaultValue("USD")
                .IsRequired();

            builder.HasIndex(p => new { p.Isin, p.PriceDate }, "UQ_Prices_Isin_Date")
                .IsUnique();
        });

        modelBuilder.Entity<FxRate>(builder =>
        {
            builder.ToTable("FxRates", "dbo");
            builder.HasKey(f => f.Id);

            builder.Property(f => f.Pair)
                .HasMaxLength(7)
                .IsRequired();

            builder.Property(f => f.RateDate)
                .HasColumnType("date")
                .IsRequired();

            builder.Property(f => f.Rate)
                .HasPrecision(18, 6)
                .IsRequired();

            builder.HasIndex(f => new { f.Pair, f.RateDate }, "UQ_FxRates_Pair_Date")
                .IsUnique();
        });
    }
}
