namespace TradeService.Domain.Entities;

public class FxRate
{
    public int Id { get; set; }
    public string Pair { get; set; } = string.Empty;
    public DateOnly RateDate { get; set; }
    public decimal Rate { get; set; }
}
