using Microsoft.FeatureManagement;
using TradeService.Domain.Dtos.Trade;
using TradeService.Domain.Interfaces;

namespace TradeService.Infrastructure.Strategies;

public class FeatureFlaggedIngestionStrategy(
    IFeatureManager featureManager,
    DeduplicatedIngestionStrategy deduplicatedStrategy,
    LegacyIngestionStrategy legacyStrategy)
    : ITradeIngestionStrategy
{
    private const string FeatureFlagName = "EnableDeduplicationEngine";

    public async Task<TradeIngestionResponse> IngestAsync(TradeIngestionRequest request, CancellationToken ct = default)
    {
        if (await featureManager.IsEnabledAsync(FeatureFlagName))
        {
            return await deduplicatedStrategy.IngestAsync(request, ct);
        }

        return await legacyStrategy.IngestAsync(request, ct);
    }
}
