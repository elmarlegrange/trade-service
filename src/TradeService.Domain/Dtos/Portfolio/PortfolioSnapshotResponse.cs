namespace TradeService.Domain.Dtos.Portfolio;

public record PortfolioSnapshotResponse(
    string AccountId,
    DateOnly SnapshotDate,
    decimal TotalValueUsd,
    List<PositionSnapshotDto> Positions
);
