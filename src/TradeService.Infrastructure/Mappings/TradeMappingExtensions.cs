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
            Isin = req.Instrument.Isin,
            Symbol = req.Instrument.Symbol,
            Side = req.Side,
            Quantity = req.Quantity,
            Price = req.Price,
            TradeDate = req.TradeDate,
            AsOf = req.AsOf,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
