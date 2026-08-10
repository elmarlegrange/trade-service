using TradeService.Domain.Dtos.Trade;

namespace TradeService.Domain.Interfaces;

public interface ITradeIngestionStrategy
{
    Task<TradeIngestionResponse> IngestAsync(TradeIngestionRequest request, CancellationToken ct = default);
}
