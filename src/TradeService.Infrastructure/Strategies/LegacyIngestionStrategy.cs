using TradeService.Domain.Dtos.Trade;
using TradeService.Domain.Interfaces;
using TradeService.Infrastructure.Mappings;
using TradeService.Infrastructure.Persistence;

namespace TradeService.Infrastructure.Strategies;

public class LegacyIngestionStrategy(TradeDbContext dbContext) : ITradeIngestionStrategy
{
    public async Task<TradeIngestionResponse> IngestAsync(TradeIngestionRequest req, CancellationToken ct = default)
    {
        var entity = req.ToEntity();
        
        dbContext.Trades.Add(entity);
        
        await dbContext.SaveChangesAsync(ct);
        
        return new TradeIngestionResponse(entity.Id, req.ExternalRef, "Created");
    }
}
