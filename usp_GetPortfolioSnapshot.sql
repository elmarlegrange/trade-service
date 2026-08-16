CREATE PROCEDURE dbo.usp_GetPortfolioSnapshot
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
     -- select into a temp table for multi-query access
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
go
