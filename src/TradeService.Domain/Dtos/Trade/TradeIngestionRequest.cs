using TradeService.Domain.Enums;

namespace TradeService.Domain.Dtos.Trade;

public record TradeIngestionRequest(
    string ExternalRef,
    string AccountId,
    string Isin,
    TradeSide Side,
    decimal Quantity,
    decimal Price,
    DateOnly TradeDate,
    DateTimeOffset AsOf
);
