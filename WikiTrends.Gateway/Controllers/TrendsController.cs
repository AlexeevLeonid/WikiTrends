using Microsoft.AspNetCore.Mvc;
using WikiTrends.Contracts.Api;
using WikiTrends.Contracts.Events;
using WikiTrends.Gateway.Services;

namespace WikiTrends.Gateway.Controllers;

[ApiController]
[Route("api/trends")]
public sealed class TrendsController : ControllerBase
{
    private readonly ITrendService _trendService;
    private readonly ILogger<TrendsController> _logger;

    public TrendsController(
        ITrendService trendService,
        ILogger<TrendsController> logger)
    {
        _trendService = trendService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<TrendsResponse>> GetTrends([FromQuery] GetTrendsRequest request, CancellationToken ct)
    {
        // TODO: 1. Провалидировать request
        // TODO: 2. Вызвать _trendService.GetTrendsAsync(request, ct)
        // TODO: 3. Если ошибка — вернуть Problem/BadRequest
        // TODO: 4. Если успех — вернуть Ok(response)
        if (request.Limit <= 0)
        {
            return BadRequest("Limit must be > 0");
        }

        var result = await _trendService.GetTrendsAsync(request, ct);
        if (!result.IsSuccess)
        {
            _logger.LogWarning("GetTrends failed: {Error}", result.Error);
            return Problem(result.Error, statusCode: StatusCodes.Status502BadGateway);
        }

        return Ok(result.Value);
    }

    [HttpGet("clusters")]
    public async Task<ActionResult<ClusterResponse>> GetClusters([FromQuery] TrendPeriod period, CancellationToken ct)
    {
        // TODO: 1. Вызвать _trendService.GetClustersAsync(period, ct)
        // TODO: 2. Вернуть Ok/Problem
        var result = await _trendService.GetClustersAsync(period, ct);
        if (!result.IsSuccess)
        {
            _logger.LogWarning("GetClusters failed: {Error}", result.Error);
            return Problem(result.Error, statusCode: StatusCodes.Status502BadGateway);
        }

        return Ok(result.Value);
    }
}
