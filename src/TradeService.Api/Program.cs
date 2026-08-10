using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;
using Serilog;
using TradeService.Domain.Interfaces;
using TradeService.Infrastructure.Persistence;
using TradeService.Infrastructure.Services;
using TradeService.Infrastructure.Strategies;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// Add Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Feature Management
builder.Services.AddFeatureManagement();

// DB Context Setup
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var useInMemory = builder.Configuration.GetValue<bool>("UseInMemoryDatabase");

builder.Services.AddDbContext<TradeDbContext>(options =>
{
    if (useInMemory)
    {
        options.UseInMemoryDatabase("TradeServiceDb");
    }
    else if (!string.IsNullOrWhiteSpace(connectionString))
    {
        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure();
        });
    }
    else
    {
        options.UseInMemoryDatabase("TradeServiceDb");
    }
});

// Ingestion Strategies & Services
builder.Services.AddScoped<DeduplicatedIngestionStrategy>();
builder.Services.AddScoped<LegacyIngestionStrategy>();
builder.Services.AddScoped<ITradeIngestionStrategy, FeatureFlaggedIngestionStrategy>();
builder.Services.AddScoped<IPortfolioSnapshotService, PortfolioService>();

var app = builder.Build();

// DB Seeding & Initialization
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<TradeDbContext>();
        await DbInitializer.SeedAsync(dbContext);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the database.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
