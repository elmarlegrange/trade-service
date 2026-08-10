namespace TradeService.Domain.Entities;

public class Price
{
    public int Id { get; set; }
    public string Isin { get; set; } = string.Empty;
    public DateOnly PriceDate { get; set; }
    public decimal PriceValue { get; set; }
    public string Currency { get; set; } = "USD";
}
