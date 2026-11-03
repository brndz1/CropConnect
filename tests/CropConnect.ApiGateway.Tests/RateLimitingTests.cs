using System.Net;
using System.Security.Claims;
using CropConnect.ApiGateway.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CropConnect.ApiGateway.Tests;

public sealed class RateLimitingTests
{
    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class FailingCounter : IRateLimitCounter
    {
        public Task<RateLimitCount> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken) =>
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis is down");
    }

    private static GatewayRateLimiter Limiter(IRateLimitCounter counter) => new(counter, NullLogger<GatewayRateLimiter>.Instance);

    #region Counter and limiter

    [Fact]
    public async Task Requests_over_the_limit_are_refused_until_the_window_ends()
    {
        var clock = new TestClock();
        var limiter = Limiter(new InMemoryRateLimitCounter(clock));

        for (var i = 0; i < 3; i++)
        {
            Assert.True((await limiter.CheckAsync("k", 3, TimeSpan.FromMinutes(1), default)).Allowed);
        }

        var refused = await limiter.CheckAsync("k", 3, TimeSpan.FromMinutes(1), default);
        Assert.False(refused.Allowed);
        Assert.Equal(0, refused.Remaining);
        Assert.Equal(TimeSpan.FromMinutes(1), refused.RetryAfter);

        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True((await limiter.CheckAsync("k", 3, TimeSpan.FromMinutes(1), default)).Allowed);
    }

    [Fact]
    public async Task Each_key_has_its_own_count()
    {
        var limiter = Limiter(new InMemoryRateLimitCounter(TimeProvider.System));

        await limiter.CheckAsync("ana", 1, TimeSpan.FromMinutes(1), default);

        Assert.False((await limiter.CheckAsync("ana", 1, TimeSpan.FromMinutes(1), default)).Allowed);
        Assert.True((await limiter.CheckAsync("rui", 1, TimeSpan.FromMinutes(1), default)).Allowed);
    }

    // Without the shared counters the gateway keeps answering, rather than refusing every call.
    [Fact]
    public async Task Requests_go_through_when_Redis_is_down()
    {
        var decision = await Limiter(new FailingCounter()).CheckAsync("k", 1, TimeSpan.FromMinutes(1), default);

        Assert.True(decision.Allowed);
    }

    #endregion

    #region Middleware

    private static (ApiRateLimitMiddleware Middleware, IServiceProvider Services, Func<int> Calls) Pipeline()
    {
        var calls = 0;
        var middleware = new ApiRateLimitMiddleware(_ =>
        {
            calls++;
            return Task.CompletedTask;
        });
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        return (middleware, services, () => calls);
    }

    private static DefaultHttpContext Request(IServiceProvider services, string path = "/api/farms", string? userId = null, string ip = "203.0.113.7")
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        if (userId is not null)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId)], "Bearer"));
        }

        return context;
    }

    private static Task InvokeAsync(ApiRateLimitMiddleware middleware, HttpContext context, GatewayRateLimiter limiter, int permitLimit) =>
        middleware.InvokeAsync(context, limiter, Options.Create(new RateLimitingOptions { PermitLimit = permitLimit }));

    [Fact]
    public async Task Call_over_the_limit_gets_429_with_Retry_After()
    {
        var (middleware, services, calls) = Pipeline();
        var limiter = Limiter(new InMemoryRateLimitCounter(TimeProvider.System));

        await InvokeAsync(middleware, Request(services), limiter, 2);
        await InvokeAsync(middleware, Request(services), limiter, 2);
        var refused = Request(services);
        await InvokeAsync(middleware, refused, limiter, 2);

        Assert.Equal(2, calls());
        Assert.Equal(StatusCodes.Status429TooManyRequests, refused.Response.StatusCode);
        Assert.True(int.Parse(refused.Response.Headers.RetryAfter!) is > 0 and <= 60);
        Assert.Equal("0", refused.Response.Headers["RateLimit-Remaining"]);
    }

    // A signed-in user is counted by account, so users behind the same address do not share a limit.
    [Fact]
    public async Task Signed_in_users_are_counted_apart_from_their_IP()
    {
        var (middleware, services, calls) = Pipeline();
        var limiter = Limiter(new InMemoryRateLimitCounter(TimeProvider.System));

        await InvokeAsync(middleware, Request(services, userId: "ana"), limiter, 1);
        await InvokeAsync(middleware, Request(services, userId: "rui"), limiter, 1);
        await InvokeAsync(middleware, Request(services), limiter, 1);

        Assert.Equal(3, calls());
    }

    [Fact]
    public async Task Only_the_API_is_limited()
    {
        var (middleware, services, calls) = Pipeline();
        var limiter = Limiter(new InMemoryRateLimitCounter(TimeProvider.System));

        for (var i = 0; i < 3; i++)
        {
            await InvokeAsync(middleware, Request(services, path: "/health"), limiter, 1);
        }

        Assert.Equal(3, calls());
    }

    #endregion
}
