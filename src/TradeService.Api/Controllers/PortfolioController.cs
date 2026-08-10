using Microsoft.AspNetCore.Mvc;
using TradeService.Domain.Dtos;
using TradeService.Domain.Dtos.Portfolio;
using TradeService.Domain.Interfaces;

namespace TradeService.Api.Controllers;

[ApiController]
[Route("api/v1/portfolios")]
public class PortfolioController(IPortfolioSnapshotService portfolioService) : ControllerBase
{
    [HttpGet("{accountId}/snapshot")]
    public async Task<ActionResult<PortfolioSnapshotResponse>> GetSnapshot(
        [FromRoute] string accountId,
        [FromQuery] DateOnly date,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return BadRequest("AccountId must be specified.");
        }

        var snapshot = await portfolioService.GetSnapshotAsync(accountId, date, ct);
        return Ok(snapshot);
    }
}
