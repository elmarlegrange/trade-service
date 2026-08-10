using System.Text.Json.Serialization;

namespace TradeService.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TradeSide
{
    Buy = 1,
    Sell = 2
}
