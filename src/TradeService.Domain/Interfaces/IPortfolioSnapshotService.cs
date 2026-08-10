using TradeService.Domain.Dtos.Portfolio;

namespace TradeService.Domain.Interfaces;

public interface IPortfolioSnapshotService
{
    Task<PortfolioSnapshotResponse> GetSnapshotAsync(string accountId, DateOnly date, CancellationToken ct = default);
}
