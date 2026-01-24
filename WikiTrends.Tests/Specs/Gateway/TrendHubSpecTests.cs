using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WikiTrends.Gateway.Hubs;

namespace WikiTrends.Tests.Specs.Gateway;

[Trait("Category", "Spec")]
public sealed class TrendHubSpecTests
{
    [Fact]
    public async Task SubscribeToTopic_AddsConnectionToGroup_AndDoesNotThrow()
    {
        var groups = new Mock<IGroupManager>(MockBehavior.Strict);
        var context = new Mock<HubCallerContext>(MockBehavior.Strict);

        context.SetupGet(c => c.ConnectionId).Returns("conn-1");

        groups
            .Setup(g => g.AddToGroupAsync("conn-1", "topic:7", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var hub = new TrendHub(NullLogger<TrendHub>.Instance)
        {
            Context = context.Object,
            Groups = groups.Object
        };

        var ex = await Record.ExceptionAsync(() => hub.SubscribeToTopic(7));
        Assert.Null(ex);

        groups.VerifyAll();
    }
}
