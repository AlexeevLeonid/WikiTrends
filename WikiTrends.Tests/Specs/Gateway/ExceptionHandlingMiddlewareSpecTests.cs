using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WikiTrends.Gateway.Middleware;

namespace WikiTrends.Tests.Specs.Gateway;

[Trait("Category", "Spec")]
public sealed class ExceptionHandlingMiddlewareSpecTests
{
    [Fact]
    public async Task InvokeAsync_WhenNextThrows_ReturnsProblemJson_AndDoesNotRethrow()
    {
        RequestDelegate next = _ => throw new InvalidOperationException("boom");

        var middleware = new ExceptionHandlingMiddleware(next, NullLogger<ExceptionHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var ex = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));
        Assert.Null(ex);

        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
    }
}
