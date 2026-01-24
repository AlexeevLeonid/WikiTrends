using Microsoft.AspNetCore.Mvc;
using WikiTrends.Contracts.Api;
using WikiTrends.Contracts.Events;
using WikiTrends.Gateway.Services;

namespace WikiTrends.Gateway.Controllers;

[ApiController]
[Route("api/topics")]
public sealed class TopicsController : ControllerBase
{
    private readonly ITopicService _topicService;
    private readonly ILogger<TopicsController> _logger;

    public TopicsController(
        ITopicService topicService,
        ILogger<TopicsController> logger)
    {
        _topicService = topicService;
        _logger = logger;
    }

    [HttpGet("{topicId:int}")]
    public async Task<ActionResult<TopicDetailResponse>> GetTopicDetails(
        [FromRoute] int topicId,
        [FromQuery] TrendPeriod period,
        CancellationToken ct)
    {
        // TODO: 1. Провалидировать topicId/period
        // TODO: 2. Вызвать _topicService.GetTopicDetailsAsync(topicId, period, ct)
        // TODO: 3. Вернуть Ok/NotFound/Problem
        if (topicId <= 0)
        {
            return BadRequest("topicId must be > 0");
        }

        var result = await _topicService.GetTopicDetailsAsync(topicId, period, ct);
        if (!result.IsSuccess)
        {
            _logger.LogWarning("GetTopicDetails failed. TopicId={TopicId}. Error={Error}", topicId, result.Error);
            return Problem(result.Error, statusCode: StatusCodes.Status502BadGateway);
        }

        return Ok(result.Value);
    }
}
