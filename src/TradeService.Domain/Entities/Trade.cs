using TradeService.Domain.Enums;

namespace TradeService.Domain.Entities;

public class Trade
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExternalRef { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string Isin { get; set; } = string.Empty;
    public TradeSide Side { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public DateOnly TradeDate { get; set; }
    public DateTimeOffset AsOf { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
