using Microsoft.AspNetCore.Mvc;

namespace WikiTrends.Gateway.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<object> Get(CancellationToken ct)
    {
        // TODO: 1. Вернуть статус сервиса (например, ok + timestamp + version)
        // TODO: 2. (опционально) проверить зависимости
        var now = DateTimeOffset.UtcNow;
        _logger.LogDebug("Health check requested at {Now}", now);
        return Ok(new { status = "ok", timestamp = now });
    }
}
