using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TradeService.Domain.Dtos.Portfolio;
using TradeService.Domain.Dtos.Trade;
using TradeService.Domain.Enums;
using TradeService.Tests.Fixtures;
using Xunit;

namespace TradeService.Tests;

public class TradeIntegrationTests(TradeServiceApiFactory factory) : IClassFixture<TradeServiceApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task TradeIngestionAndSnapshot_FullLifecycle_Succeeds()
    {
        // Arrange
        var accountId = "ACC-E2E-100";
        var externalRef = "TRD-E2E-888";
        var isin = "US0378331005";
        var tradeDate = new DateOnly(2025, 3, 1);
        var initialAsOf = new DateTimeOffset(2025, 3, 1, 9, 0, 0, TimeSpan.Zero);
        var duplicateAsOf = new DateTimeOffset(2025, 3, 1, 8, 50, 0, TimeSpan.Zero);
        var correctionAsOf = new DateTimeOffset(2025, 3, 1, 10, 30, 0, TimeSpan.Zero);

        // 1. Submit Initial Trade (100 units @ $150)
        var initialTradeRequest = new TradeIngestionRequest(
            ExternalRef: externalRef,
            AccountId: accountId,
            Isin: isin,
            Side: TradeSide.Buy,
            Quantity: 100m,
            Price: 150.00m,
            TradeDate: tradeDate,
            AsOf: initialAsOf
        );

        var initialResponse = await _client.PostAsJsonAsync("/api/v1/trades", initialTradeRequest);
        initialResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var initialResponseBody = await initialResponse.Content.ReadFromJsonAsync<TradeIngestionResponse>();
        initialResponseBody.Should().NotBeNull();
        initialResponseBody!.Status.Should().Be("Created");
        initialResponseBody.ExternalRef.Should().Be(externalRef);
        initialResponseBody.InternalId.Should().NotBeEmpty();

        // 2. Submit Duplicate Trade (same ExternalRef, equal or earlier AsOf) -> Expect 200 OK / IgnoredDuplicate
        var duplicateTradeRequest = new TradeIngestionRequest(
            ExternalRef: externalRef,
            AccountId: accountId,
            Isin: isin,
            Side: TradeSide.Buy,
            Quantity: 100m,
            Price: 150.00m,
            TradeDate: tradeDate,
            AsOf: duplicateAsOf
        );

        var duplicateResponse = await _client.PostAsJsonAsync("/api/v1/trades", duplicateTradeRequest);
        duplicateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var duplicateResponseBody = await duplicateResponse.Content.ReadFromJsonAsync<TradeIngestionResponse>();
        duplicateResponseBody.Should().NotBeNull();
        duplicateResponseBody!.Status.Should().Be("IgnoredDuplicate");
        duplicateResponseBody.ExternalRef.Should().Be(externalRef);

        // 3. Submit Correction Trade (same ExternalRef, later AsOf, updated Quantity = 120) -> Expect 201 Created / CorrectionApplied
        var correctionTradeRequest = new TradeIngestionRequest(
            ExternalRef: externalRef,
            AccountId: accountId,
            Isin: isin,
            Side: TradeSide.Buy,
            Quantity: 120m,
            Price: 150.00m,
            TradeDate: tradeDate,
            AsOf: correctionAsOf
        );

        var correctionResponse = await _client.PostAsJsonAsync("/api/v1/trades", correctionTradeRequest);
        correctionResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var correctionResponseBody = await correctionResponse.Content.ReadFromJsonAsync<TradeIngestionResponse>();
        correctionResponseBody.Should().NotBeNull();
        correctionResponseBody!.Status.Should().Be("CorrectionApplied");
        correctionResponseBody.ExternalRef.Should().Be(externalRef);

        // 4. Verify Portfolio Snapshot
        var snapshotResponse = await _client.GetAsync($"/api/v1/portfolios/{accountId}/snapshot?date=2025-03-01");
        snapshotResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var snapshotResponseBody = await snapshotResponse.Content.ReadFromJsonAsync<PortfolioSnapshotResponse>();
        snapshotResponseBody.Should().NotBeNull();
        snapshotResponseBody!.AccountId.Should().Be(accountId);
        snapshotResponseBody.SnapshotDate.Should().Be(tradeDate);
        snapshotResponseBody.TotalValueUsd.Should().Be(18000.00m); // 120 units * $150.00 price
        snapshotResponseBody.Positions.Should().HaveCount(1);

        var position = snapshotResponseBody.Positions.Single();
        position.Isin.Should().Be("US0378331005");
        position.Quantity.Should().Be(120m);
        position.AverageUnitCostUsd.Should().Be(150.00m);
        position.PriceUsd.Should().Be(150.00m);
        position.MarketValueUsd.Should().Be(18000.00m);
        position.UnrealizedPlUsd.Should().Be(0.00m);
    }
}
