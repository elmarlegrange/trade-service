# Trade Service (.NET 8 Web API)

An ASP.NET Core (.NET 8) Web API for trade ingestion, append-only deduplication and correction handling, and portfolio snapshot calculation.

## Architecture & Project Structure

- `src/TradeService.Domain`: Core domain entities (`Trade`, `Price`, `FxRate`), DTOs (`TradeIngestionRequest`, `PortfolioSnapshotResponse`, etc.), and domain interfaces (`ITradeIngestionStrategy`, `IPortfolioSnapshotService`).
- `src/TradeService.Infrastructure`: EF Core `TradeDbContext`, database migrations, seed data initializer, portfolio calculation logic, and strategy implementations (`DeduplicatedIngestionStrategy`, `LegacyIngestionStrategy`, `FeatureFlaggedIngestionStrategy`).
- `src/TradeService.Api`: ASP.NET Core Web API controllers (`TradesController`, `PortfolioController`), and configuration.
- `tests/TradeService.Tests`: End-to-end xUnit integration tests suite using `WebApplicationFactory` and FluentAssertions.

---

## Prerequisites

- .NET 8.0 SDK
- SQL Server or LocalDB (Optional - defaults to EF Core InMemory database when SQL Server is omitted)

---

## Quick Start (Local Setup)

### 1. Restore & Build Solution

```bash
dotnet restore TradeService.sln
dotnet build TradeService.sln
```

### 2. Run Integration Tests

Run the full end-to-end integration test suite verifying trade ingestion lifecycle (initial -> duplicate -> correction) and snapshot calculations:

```bash
dotnet test TradeService.sln
```

### 3. Run Web API Service

```bash
dotnet run --project src/TradeService.Api/TradeService.Api.csproj
```

By default, the Web API will start and expose Swagger UI at:
- `http://localhost:5000/swagger` or `https://localhost:5001/swagger`

---

## API Endpoints

### 1. Ingest Trade Event
- **Endpoint**: `POST /api/v1/trades`
- **Headers**: `Content-Type: application/json`

**Sample Request Body (Initial Trade)**:
```json
{
  "externalRef": "TRD-1002",
  "accountId": "ACC-001",
  "isin": "US0378331005",
  "side": "Buy",
  "quantity": 10,
  "price": 150,
  "tradeDate": "2026-08-16",
  "asOf": "2026-08-16T20:45:39.998Z"
}
```

**Responses**:
- Initial Trade -> HTTP `201 Created` (`"status": "Created"`)
- Duplicate Trade (`AsOf` <= existing) -> HTTP `200 OK` (`"status": "IgnoredDuplicate"`)
- Correction Trade (`AsOf` > existing) -> HTTP `201 Created` (`"status": "CorrectionApplied"`)

---

### 2. Get Portfolio Snapshot
- **Endpoint**: `GET /api/v1/portfolios/{accountId}/snapshot?date=2026-08-16`

**Sample Request**:
**Endpoint**: `GET /api/v1/portfolios/ACC-001/snapshot?date=2026-08-16`

**Sample Response Body**:
```json
{
  "accountId": "ACC-001",
  "snapshotDate": "2026-08-16",
  "totalValueUsd": 18000,
  "positions": [
    {
      "isin": "US0378331005",
      "quantity": 120,
      "averageUnitCostUsd": 150,
      "priceUsd": 150,
      "marketValueUsd": 18000,
      "unrealizedPlUsd": 0
    }
  ]
}
```

---

## Configuration Options (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=TradeServiceDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
  },
  "FeatureManagement": {
    "EnableDeduplicationEngine": true
  }
}
```

Set `EnableDeduplicationEngine` to `true` or `false` to toggle between `DeduplicatedIngestionStrategy` and `LegacyIngestionStrategy` dynamically via `Microsoft.FeatureManagement`.
