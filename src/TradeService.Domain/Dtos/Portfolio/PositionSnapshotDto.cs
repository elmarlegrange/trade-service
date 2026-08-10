namespace TradeService.Domain.Dtos.Portfolio;

public record PositionSnapshotDto(
    string Isin,
    string Symbol,
    decimal Quantity,
    decimal AverageUnitCostUsd,
    decimal PriceUsd,
    decimal MarketValueUsd,
    decimal UnrealizedPlUsd
);
