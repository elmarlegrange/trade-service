using Microsoft.EntityFrameworkCore;
using TradeService.Domain.Dtos.Portfolio;
using TradeService.Domain.Enums;
using TradeService.Domain.Interfaces;
using TradeService.Infrastructure.Persistence;

namespace TradeService.Infrastructure.Services;

public class PortfolioService(TradeDbContext dbContext) : IPortfolioSnapshotService
{
    public async Task<PortfolioSnapshotResponse> GetSnapshotAsync(string accountId, DateOnly date, CancellationToken ct = default)
    {
        var rawTrades = await dbContext.Trades
            .Where(t => t.AccountId == accountId && t.TradeDate <= date)
            .ToListAsync(ct);
        
        var activeTrades = rawTrades
            .GroupBy(t => t.ExternalRef)
            .Select(g => g.OrderByDescending(t => t.AsOf).ThenByDescending(t => t.CreatedAtUtc).First())
            .ToList();

        var positions = new List<PositionSnapshotDto>();
        
        var instrumentGroups = activeTrades
            .GroupBy(t => new { t.Isin, t.Symbol })
            .ToList();

        foreach (var group in instrumentGroups)
        {
            var orderedTrades = group
                .OrderBy(t => t.TradeDate)
                .ThenBy(t => t.AsOf)
                .ThenBy(t => t.CreatedAtUtc)
                .ToList();

            var runningQty = 0m;
            var totalCostBasis = 0m;

            foreach (var trade in orderedTrades)
            {
                switch (trade.Side)
                {
                    case TradeSide.Buy:
                        totalCostBasis += trade.Quantity * trade.Price;
                        runningQty += trade.Quantity;
                        
                        break;
                    case TradeSide.Sell:
                    {
                        if (runningQty > 0)
                        {
                            var avgCostBeforeSell = totalCostBasis / runningQty;
                            
                            runningQty -= trade.Quantity;
                            
                            if (runningQty <= 0)
                            {
                                runningQty = 0m;
                                totalCostBasis = 0m;
                            }
                            else
                            {
                                totalCostBasis = runningQty * avgCostBeforeSell;
                            }
                        }

                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (runningQty <= 0)
            {
                continue;
            }

            var averageUnitCostUsd = runningQty > 0 ? (totalCostBasis / runningQty) : 0m;
            
            var priceRecord = await dbContext.Prices
                .Where(p => p.Isin == group.Key.Isin && p.PriceDate <= date)
                .OrderByDescending(p => p.PriceDate)
                .FirstOrDefaultAsync(ct);

            var priceUsd = 0m;

            if (priceRecord != null)
            {
                var currency = priceRecord.Currency.Trim();
                
                if (string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase))
                {
                    priceUsd = priceRecord.PriceValue;
                }
                else
                {
                    var pair = $"USD-{currency}";
                    var fxRecord = await dbContext.FxRates
                        .Where(f => f.Pair == pair && f.RateDate <= date)
                        .OrderByDescending(f => f.RateDate)
                        .FirstOrDefaultAsync(ct);

                    if (fxRecord != null && fxRecord.Rate > 0)
                    {
                        priceUsd = priceRecord.PriceValue / fxRecord.Rate;
                    }
                    else
                    {
                        priceUsd = priceRecord.PriceValue;
                    }
                }
            }

            var marketValueUsd = runningQty * priceUsd;
            var unrealizedPlUsd = marketValueUsd - (runningQty * averageUnitCostUsd);

            positions.Add(new PositionSnapshotDto(
                Isin: group.Key.Isin,
                Symbol: group.Key.Symbol,
                Quantity: runningQty,
                AverageUnitCostUsd: Math.Round(averageUnitCostUsd, 4),
                PriceUsd: Math.Round(priceUsd, 4),
                MarketValueUsd: Math.Round(marketValueUsd, 4),
                UnrealizedPlUsd: Math.Round(unrealizedPlUsd, 4)
            ));
        }

        var totalValueUsd = positions.Sum(p => p.MarketValueUsd);

        return new PortfolioSnapshotResponse(
            AccountId: accountId,
            SnapshotDate: date,
            TotalValueUsd: Math.Round(totalValueUsd, 4),
            Positions: positions
        );
    }
}
