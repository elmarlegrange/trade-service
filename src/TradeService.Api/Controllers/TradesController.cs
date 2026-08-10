using Microsoft.AspNetCore.Mvc;
using TradeService.Domain.Dtos;
using TradeService.Domain.Dtos.Trade;
using TradeService.Domain.Enums;
using TradeService.Domain.Interfaces;

namespace TradeService.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class TradesController(ITradeIngestionStrategy ingestionStrategy) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> IngestTrade([FromBody] TradeIngestionRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(typeof(TradeSide), request.Side))
        {
            return BadRequest($"Invalid trade side '{request.Side}'. Allowed values are 'Buy' or 'Sell'.");
        }

        var response = await ingestionStrategy.IngestAsync(request, ct);

        if (string.Equals(response.Status, "IgnoredDuplicate", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(response);
        }

        return CreatedAtAction(nameof(IngestTrade), new { id = response.InternalId }, response);
    }
}
