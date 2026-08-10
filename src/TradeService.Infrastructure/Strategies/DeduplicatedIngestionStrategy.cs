using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradeService.Domain.Dtos.Trade;
using TradeService.Domain.Interfaces;
using TradeService.Infrastructure.Mappings;
using TradeService.Infrastructure.Persistence;

namespace TradeService.Infrastructure.Strategies;

public class DeduplicatedIngestionStrategy(TradeDbContext dbContext, ILogger<DeduplicatedIngestionStrategy> logger)
    : ITradeIngestionStrategy
{
    public async Task<TradeIngestionResponse> IngestAsync(TradeIngestionRequest req, CancellationToken ct = default)
    {
        var latestTrade = await dbContext.Trades
            .Where(t => t.ExternalRef == req.ExternalRef)
            .OrderByDescending(t => t.AsOf)
            .FirstOrDefaultAsync(ct);

        if (latestTrade != null && req.AsOf <= latestTrade.AsOf)
        {
            logger.LogInformation("Ignored duplicate trade payload for ExternalRef: {Ref}", req.ExternalRef);
            
            return new TradeIngestionResponse(latestTrade.Id, req.ExternalRef, "IgnoredDuplicate");
        }

        var entity = req.ToEntity();
        
        dbContext.Trades.Add(entity);
        
        await dbContext.SaveChangesAsync(ct);

        var status = latestTrade == null 
            ? "Created" 
            : "CorrectionApplied";
        
        return new TradeIngestionResponse(entity.Id, req.ExternalRef, status);
    }
}
