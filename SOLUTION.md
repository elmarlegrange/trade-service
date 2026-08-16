# SOLUTION.md - Trade Ingestion & Portfolio Snapshot Service

## 1. Design & Trade-offs

### Architecture Overview
The solution is built using .NET 8 (ASP.NET Core Web API) and EF Core 8 across clean, focused layers:
- **`TradeService.Domain`**: Pure entities (`Trade`, `Price`, `FxRate`), `TradeSide` enum (`Buy`, `Sell`), DTOs, and interfaces. No external dependencies.
- **`TradeService.Infrastructure`**: EF Core `TradeDbContext`, migrations, seed data, portfolio calculations, and ingestion strategies.
- **`TradeService.Api`**: Controllers (`TradesController`, `PortfolioController`), Serilog logging, Swagger UI, and dependency injection configuration.
- **`TradeService.Tests`**: End-to-end integration tests using `WebApplicationFactory`.

### Key Design Trade-offs

#### Append-Only Event Ledger vs In-Place Updates
- **Decision**: Persist every valid trade submission and correction as a new immutable row in `dbo.Trades` with its `AsOf` timestamp.
- **Trade-off**: Slightly higher storage usage in exchange for complete historical auditability, zero row-level lock contention on updates, and straightforward point-in-time state reconstruction.
- **Deduplication Logic**:
  - **Re-sends** (`same ExternalRef`, `AsOf <= existing`): Ignored without database writes $\rightarrow$ returns `200 OK` (`"IgnoredDuplicate"`).
  - **Corrections** (`same ExternalRef`, `AsOf > existing`): Appends a new version row $\rightarrow$ returns `201 Created` (`"CorrectionApplied"`).
  - **Initial Trade**: Inserts new row $\rightarrow$ returns `201 Created` (`"Created"`).

---

## 2. Safe Rollout & Disablement Strategy

The ingestion logic uses the **Strategy Pattern** paired with **`Microsoft.FeatureManagement`**:

- **`ITradeIngestionStrategy`**: Interface implemented by:
  - `DeduplicatedIngestionStrategy`: New append-only deduplication and correction logic.
  - `LegacyIngestionStrategy`: Fallback direct write without deduplication checks.
  - `FeatureFlaggedIngestionStrategy`: Decorator that checks feature flag `"EnableDeduplicationEngine"` at runtime.

### Rollout Plan:
1. **Dark Launch**: Deploy with `EnableDeduplicationEngine: false` (runs legacy fallback).
2. **Canary / Targeted Enablement**: Enable `EnableDeduplicationEngine: true` in staging or for targeted traffic while monitoring structured logs.
3. **Full Enablement**: Enable globally in production config.
4. **Instant Rollback**: If unexpected issues arise, toggle `EnableDeduplicationEngine: false` in `appsettings.json` or Azure App Configuration without restarting or redeploying code.

---

## 3. Assumptions & Financial Formulas

1. **Base Currency**: All snapshot values and portfolio totals are reported in **USD**.
2. **Foreign Exchange (FX)**: Non-USD prices (e.g. `ZAR`) are converted to USD using daily rates from `dbo.FxRates` (e.g., `Pair = "USD-ZAR"`, `PriceUsd = Price / Rate`). If currency is `USD`, price is used directly.
3. **Valuation Date Rule**: Snapshot on date $D$ includes all active trades where `TradeDate <= D`. Multiple versions of the same `ExternalRef` resolve to the version with the latest `AsOf` timestamp on or before $D$.
4. **Rounding & Precision**:
   - Internal storage: `DECIMAL(18, 4)` for prices/quantities, `DECIMAL(18, 6)` for FX rates.
   - Output responses round values to 4 decimal places (compatible with 2-decimal display).
5. **Cost Basis Derivation (Weighted Average Cost)**:
   - **`BUY`**: Adds to position quantity and total cost basis:
     $$\text{Avg Cost} = \frac{(\text{Current Qty} \times \text{Current Avg Cost}) + (\text{Buy Qty} \times \text{Buy Price})}{\text{Current Qty} + \text{Buy Qty}}$$
   - **`SELL`**: Reduces position quantity while maintaining current average unit cost.
6. **Valuations**:
   - $\text{Market Value USD} = \text{Quantity} \times \text{Price USD}$
   - $\text{Unrealized P/L USD} = \text{Market Value USD} - (\text{Quantity} \times \text{Average Unit Cost USD})$
   - $\text{Total Value USD} = \sum \text{Market Value USD}$

---

## 4. Task 2: SQL Deep Dive

### 4.1 SQL Stored Procedure: `dbo.usp_GetPortfolioSnapshot`

```sql
CREATE OR ALTER PROCEDURE dbo.usp_GetPortfolioSnapshot
    @AccountId NVARCHAR(50),
    @SnapshotDate DATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Deduplicate Trades: latest AsOf per ExternalRef up to @SnapshotDate
    ;WITH RankedTrades AS (
        SELECT
            t.Id, t.ExternalRef, t.AccountId, t.Isin, t.Side,
            t.Quantity, t.Price, t.TradeDate, t.AsOf, t.CreatedAt,
            ROW_NUMBER() OVER (
                PARTITION BY t.ExternalRef
                ORDER BY t.AsOf DESC, t.CreatedAt DESC
            ) AS VersionRank
        FROM dbo.Trades t
        WHERE t.AccountId = @AccountId
          AND t.TradeDate <= @SnapshotDate
    ),
    ActiveTrades AS (
        SELECT * FROM RankedTrades WHERE VersionRank = 1
    ),
    -- Aggregate Positions & Average Unit Cost Basis per Instrument
    PositionAggregates AS (
        SELECT
            at.Isin,
            SUM(CASE WHEN at.Side IN ('Buy', 'BUY') THEN at.Quantity
                     WHEN at.Side IN ('Sell', 'SELL') THEN -at.Quantity
                     ELSE 0 END) AS NetQuantity,
            SUM(CASE WHEN at.Side IN ('Buy', 'BUY') THEN at.Quantity * at.Price ELSE 0 END) AS TotalBuySpend,
            SUM(CASE WHEN at.Side IN ('Buy', 'BUY') THEN at.Quantity ELSE 0 END) AS TotalBuyQuantity
        FROM ActiveTrades at
        GROUP BY at.Isin
        HAVING SUM(CASE WHEN at.Side IN ('Buy', 'BUY') THEN at.Quantity
                        WHEN at.Side IN ('Sell', 'SELL') THEN -at.Quantity
                        ELSE 0 END) > 0
    ),
    -- Resolve Latest Market Price on or before @SnapshotDate
    RankedPrices AS (
        SELECT
            p.Isin, p.Price, p.Currency, p.PriceDate,
            ROW_NUMBER() OVER (PARTITION BY p.Isin ORDER BY p.PriceDate DESC) AS PriceRank
        FROM dbo.Prices p
        WHERE p.PriceDate <= @SnapshotDate
    ),
    LatestPrices AS (
        SELECT * FROM RankedPrices WHERE PriceRank = 1
    ),
    -- Resolve Latest FX Rate on or before @SnapshotDate
    RankedFxRates AS (
        SELECT
            fx.Pair, fx.Rate, fx.RateDate,
            ROW_NUMBER() OVER (PARTITION BY fx.Pair ORDER BY fx.RateDate DESC) AS FxRank
        FROM dbo.FxRates fx
        WHERE fx.RateDate <= @SnapshotDate
    ),
    LatestFxRates AS (
        SELECT * FROM RankedFxRates WHERE FxRank = 1
    ),
    -- Value Positions in USD
    ValuedPositions AS (
        SELECT
            pa.Isin,
            pa.NetQuantity AS Quantity,
            CAST(ROUND(pa.TotalBuySpend / NULLIF(pa.TotalBuyQuantity, 0), 4) AS DECIMAL(18, 4)) AS AverageUnitCostUsd,
            CAST(ROUND(
                    CASE
                        WHEN lp.Currency = 'USD' OR lp.Currency IS NULL THEN ISNULL(lp.Price, 0.0)
                        ELSE ISNULL(lp.Price, 0.0) / NULLIF(lfx.Rate, 1.0)
                    END, 4) AS DECIMAL(18, 4)) AS PriceUsd,
            CAST(ROUND(
                    pa.NetQuantity *
                    CASE
                        WHEN lp.Currency = 'USD' OR lp.Currency IS NULL THEN ISNULL(lp.Price, 0.0)
                        ELSE ISNULL(lp.Price, 0.0) / NULLIF(lfx.Rate, 1.0)
                    END, 4) AS DECIMAL(18, 4)) AS MarketValueUsd,
            CAST(ROUND(
                    (pa.NetQuantity *
                     CASE
                         WHEN lp.Currency = 'USD' OR lp.Currency IS NULL THEN ISNULL(lp.Price, 0.0)
                         ELSE ISNULL(lp.Price, 0.0) / NULLIF(lfx.Rate, 1.0)
                     END) - (pa.NetQuantity * (pa.TotalBuySpend / NULLIF(pa.TotalBuyQuantity, 0))), 4) AS DECIMAL(18, 4)) AS UnrealizedPlUsd
        FROM PositionAggregates pa
        LEFT JOIN LatestPrices lp ON pa.Isin = lp.Isin
        LEFT JOIN LatestFxRates lfx ON lfx.Pair = CONCAT('USD-', LTRIM(RTRIM(lp.Currency)))
    )
    -- Materialize into a temp table for multi-query access
    SELECT *
    INTO #ValuedPositions
    FROM ValuedPositions;

    -- Result Set 1: Instrument-Level Positions
    SELECT Isin, Quantity, AverageUnitCostUsd, PriceUsd, MarketValueUsd, UnrealizedPlUsd
    FROM #ValuedPositions
    ORDER BY Isin;

    -- Result Set 2: Account-Level Summary
    SELECT
        @AccountId AS AccountId,
        @SnapshotDate AS SnapshotDate,
        ISNULL(SUM(MarketValueUsd), 0.00) AS TotalMarketValueUsd,
        ISNULL(SUM(UnrealizedPlUsd), 0.00) AS TotalUnrealizedPlUsd,
        COUNT(Isin) AS PositionCount
    FROM #ValuedPositions;

    DROP TABLE IF EXISTS #ValuedPositions;

END;
GO
```

### 4.2 Supporting Index Definitions (DDL)

```sql
-- Fast single-trade deduplication lookup on ingestion
CREATE NONCLUSTERED INDEX IX_Trades_ExternalRef_AsOf 
ON dbo.Trades (ExternalRef, AsOf DESC) 
INCLUDE (AccountId, Isin, Side, Quantity, Price, TradeDate, CreatedAt);

-- Fast account snapshot query covering all aggregation columns
CREATE NONCLUSTERED INDEX IX_Trades_AccountId_TradeDate 
ON dbo.Trades (AccountId, TradeDate) 
INCLUDE (ExternalRef, Isin, Side, Quantity, Price, AsOf, CreatedAt);

-- Fast price & FX lookups by date
CREATE UNIQUE NONCLUSTERED INDEX UQ_Prices_Isin_Date ON dbo.Prices (Isin, PriceDate) INCLUDE (Price, Currency);
CREATE UNIQUE NONCLUSTERED INDEX UQ_FxRates_Pair_Date ON dbo.FxRates (Pair, RateDate) INCLUDE (Rate);
```

### 4.3 High Volume Performance, SARGability & Archival

- **Covering Indexes**: `IX_Trades_AccountId_TradeDate` includes all columns required by the CTE, enabling a single **Index Seek** without expensive Bookmark Lookups.
- **SARGability**: Predicates (`TradeDate <= @SnapshotDate` and `AccountId = @AccountId`) use raw column comparisons without scalar functions, allowing index seeks on B-Trees.
- **Table Partitioning**: In a high-volume database (millions of trades), partition `dbo.Trades` by `TradeDate` (e.g. monthly ranges). This allows:
  - **Partition Pruning**: Queries only scan partitions matching the requested date range.
  - **Archival via Partition Switching**: Old data (> 7 years) can be moved to cold storage tables in milliseconds using metadata-only `ALTER TABLE ... SWITCH PARTITION` operations.
- **Read Replicas**: Direct ad-hoc reporting queries to a read-only replica to keep OLTP ingestion performance unhindered.

---

## 5. .NET Framework 4.8 vs Modern .NET Considerations

| Area | .NET Framework 4.8 | Modern .NET (.NET 8) | Integration / Migration Approach |
| :--- | :--- | :--- | :--- |
| **Data Access** | **EF6** / ADO.NET.<br>Heavy change tracker, EDMX XML mapping, limited async support. | **EF Core 8**.<br>Compiled models, native `DateOnly`/`TimeOnly`, lightweight tracking, batch queries. | Target **.NET Standard 2.0** for shared contracts/DTOs; isolate EF Core 8 inside modern API services. |
| **Dependency Injection** | Third-party containers required (Autofac, Unity, Ninject). | Built-in **`Microsoft.Extensions.DependencyInjection`** with keyed services. | Register services via standard `IServiceCollection` abstractions. |
| **Configuration & Feature Flags** | Static `web.config` / `ConfigurationManager`. Requires app pool recycle to update. | **`Microsoft.Extensions.Configuration`** & **`Microsoft.FeatureManagement`** with live reload. | Legacy apps can call the .NET 8 API or consume JSON configuration via `Microsoft.Extensions.Configuration` NuGet packages. |
| **Logging** | log4net / NLog with string formatting. | **Serilog** structured logging with semantic templates and OpenTelemetry support. | Send structured JSON logs to a centralized log sink (Seq, Elasticsearch, Application Insights). |
| **Hosting & Web Pipeline** | **IIS + System.Web** (monolithic, high memory per request). | **Kestrel** (cross-platform, low-allocation async pipeline). | Use the **Strangler Fig Pattern**: run .NET 8 services in containers behind an API Gateway (YARP / Envoy) while legacy monolith routes traffic. |
