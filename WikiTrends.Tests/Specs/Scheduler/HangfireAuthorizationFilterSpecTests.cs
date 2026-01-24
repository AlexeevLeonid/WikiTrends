using WikiTrends.Scheduler.Configuration;

namespace WikiTrends.Tests.Specs.Scheduler;

[Trait("Category", "Spec")]
public sealed class HangfireAuthorizationFilterSpecTests
{
    [Fact]
    public void Authorize_WhenContextIsNull_ReturnsTrue_AndDoesNotThrow()
    {
        var filter = new HangfireAuthorizationFilter();

        var ex = Record.Exception(() => filter.Authorize(null!));
        Assert.Null(ex);

        var allowed = filter.Authorize(null!);
        Assert.True(allowed);
    }
}
