using TradeService.Domain.Entities;

namespace TradeService.Infrastructure.Persistence;

public static class DbInitializer
{
    public static async Task SeedAsync(TradeDbContext dbContext)
    {
        await dbContext.Database.EnsureCreatedAsync();

        if (!dbContext.Prices.Any())
        {
            dbContext.Prices.Add(new Price
            {
                Isin = "US0378331005",
                PriceDate = new DateOnly(2025, 3, 1),
                PriceValue = 150.00m,
                Currency = "USD"
            });
        }

        if (!dbContext.FxRates.Any())
        {
            dbContext.FxRates.Add(new FxRate
            {
                Pair = "USD-ZAR",
                RateDate = new DateOnly(2025, 3, 1),
                Rate = 18.50m
            });
        }

        await dbContext.SaveChangesAsync();
    }
}
