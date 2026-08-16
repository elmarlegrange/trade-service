using Microsoft.AspNetCore.Mvc;
using TradeService.Domain.Dtos.Trade;
using TradeService.Domain.Interfaces;

namespace TradeService.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class TradesController(ITradeIngestionStrategy ingestionStrategy) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> IngestTrade(
        [FromBody] TradeIngestionRequest request, 
        CancellationToken ct)
    {
        var response = await ingestionStrategy.IngestAsync(request, ct);

        if (string.Equals(response.Status, "IgnoredDuplicate", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(response);
        }

        return CreatedAtAction(nameof(IngestTrade), new { id = response.InternalId }, response);
    }
}