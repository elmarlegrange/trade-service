namespace TradeService.Domain.Dtos.Trade;

public record TradeIngestionResponse(
    Guid InternalId,
    string ExternalRef,
    string Status
);
