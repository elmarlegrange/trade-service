using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using TradeService.Domain.Dtos.Portfolio;
using TradeService.Domain.Interfaces;

namespace TradeService.Api.Controllers;

[ApiController]
[Route("api/v1/portfolios")]
public class PortfolioController(IPortfolioSnapshotService portfolioService) : ControllerBase
{
    [HttpGet("{accountId}/snapshot")]
    public async Task<ActionResult<PortfolioSnapshotResponse>> GetSnapshot(
        [FromRoute, Required(AllowEmptyStrings = false)] string accountId,
        [FromQuery, Required] DateOnly date,
        CancellationToken ct)
    {
        var snapshot = await portfolioService.GetSnapshotAsync(accountId, date, ct);
        return Ok(snapshot);
    }
}