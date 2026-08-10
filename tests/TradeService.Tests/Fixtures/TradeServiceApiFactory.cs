using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradeService.Infrastructure.Persistence;

namespace TradeService.Tests.Fixtures;

public class TradeServiceApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UseInMemoryDatabase"] = "true",
                ["FeatureManagement:EnableDeduplicationEngine"] = "true"
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<TradeDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<TradeDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName);
            });

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TradeDbContext>();
            db.Database.EnsureCreated();
            DbInitializer.SeedAsync(db).GetAwaiter().GetResult();
        });
    }
}
