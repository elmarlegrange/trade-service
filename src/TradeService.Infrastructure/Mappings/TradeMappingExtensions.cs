using TradeService.Domain.Dtos.Trade;
using TradeService.Domain.Entities;

namespace TradeService.Infrastructure.Mappings;

public static class TradeMappingExtensions
{
    public static Trade ToEntity(this TradeIngestionRequest req)
    {
        return new Trade
        {
            Id = Guid.NewGuid(),
            ExternalRef = req.ExternalRef,
            AccountId = req.AccountId,
            Isin = req.Isin,
            Side = req.Side,
            Quantity = req.Quantity,
            Price = req.Price,
            TradeDate = req.TradeDate,
            AsOf = req.AsOf,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
