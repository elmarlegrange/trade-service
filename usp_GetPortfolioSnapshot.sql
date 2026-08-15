CREATE OR ALTER PROCEDURE dbo.usp_GetPortfolioSnapshot
    @AccountId VARCHAR(50),
    @ValuationDate DATE,
    @AsOfCutoff DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @AsOfCutoff = ISNULL(@AsOfCutoff, SYSUTCDATETIME());

    WITH LatestTrades AS (
        SELECT
            t.ExternalRef,
            t.AccountId,
            t.Isin,
            t.Side,
            t.Quantity,
            t.Price,
            t.TradeDate,
            t.AsOf,
            ROW_NUMBER() OVER (
                PARTITION BY t.ExternalRef
                ORDER BY t.AsOf DESC, t.Id DESC
                ) AS RowNum
        FROM dbo.Trades t
        WHERE t.AccountId = @AccountId
          AND t.TradeDate <= @ValuationDate
          AND t.AsOf <= @AsOfCutoff
    ),
         ValidActiveTrades AS (
             SELECT
                 ExternalRef,
                 AccountId,
                 Isin,
                 Side,
                 Quantity,
                 Price,
                 CASE WHEN Side = 'BUY' THEN Quantity ELSE -Quantity END AS SignedQuantity,
                 CASE WHEN Side = 'BUY' THEN -(Quantity * Price) ELSE (Quantity * Price) END AS CashFlow
             FROM LatestTrades
             WHERE RowNum = 1
         ),
         PositionAggregates AS (
             SELECT
                 Isin,
                 SUM(SignedQuantity) AS TotalQuantity,
                 CASE
                     WHEN SUM(CASE WHEN Side = 'BUY' THEN Quantity ELSE 0 END) > 0
                         THEN SUM(CASE WHEN Side = 'BUY' THEN Quantity * Price ELSE 0 END)
                         / SUM(CASE WHEN Side = 'BUY' THEN Quantity ELSE 0 END)
                     ELSE 0
                     END AS UnitCost
             FROM ValidActiveTrades
             GROUP BY Isin
             HAVING SUM(SignedQuantity) <> 0
         ),
         LatestPrices AS (
             SELECT
                 p.Isin,
                 p.Price AS MarketPrice,
                 ROW_NUMBER() OVER (
                     PARTITION BY p.Isin
                     ORDER BY p.PriceDate DESC
                     ) AS PriceRank
             FROM dbo.Prices p
             WHERE p.PriceDate <= @ValuationDate
         )

    SELECT
        pa.Isin,
        pa.TotalQuantity AS Quantity,
        ROUND(pa.UnitCost, 4) AS UnitCostUSD,
        ISNULL(lp.MarketPrice, 0) AS MarketPriceUSD,
        ROUND(pa.TotalQuantity * ISNULL(lp.MarketPrice, 0), 2) AS MarketValueUSD,
        ROUND((pa.TotalQuantity * ISNULL(lp.MarketPrice, 0)) - (pa.TotalQuantity * pa.UnitCost), 2) AS UnrealizedPnLUSD
    INTO #InstrumentSummary
    FROM PositionAggregates pa
             LEFT JOIN LatestPrices lp ON pa.Isin = lp.Isin AND lp.PriceRank = 1;

    SELECT
        Isin,
        Quantity,
        UnitCostUSD,
        MarketPriceUSD,
        MarketValueUSD,
        UnrealizedPnLUSD
    FROM #InstrumentSummary;

    SELECT
        @AccountId AS AccountId,
        @ValuationDate AS ValuationDate,
        ISNULL(SUM(MarketValueUSD), 0) AS TotalMarketValueUSD
    FROM #InstrumentSummary;

    DROP TABLE #InstrumentSummary;
END;
GO