using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CropConnect.ApiGateway.Infrastructure;

public readonly record struct RateLimitCount(long Count, TimeSpan ResetsIn);

public readonly record struct RateLimitDecision(bool Allowed, int Limit, long Remaining, TimeSpan RetryAfter)
{
    public static RateLimitDecision Unlimited(int limit) => new(true, limit, limit, TimeSpan.Zero);
}

// Counts the requests of one key in the current window and says when the window ends.
public interface IRateLimitCounter
{
    Task<RateLimitCount> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken);
}

// Shared by every replica of the gateway. One script, so the increment and the expiry are atomic.
public sealed class RedisRateLimitCounter : IRateLimitCounter
{
    private const string Script = """
        local count = redis.call('INCR', KEYS[1])
        local ttl = redis.call('PTTL', KEYS[1])
        if ttl < 0 then
          redis.call('PEXPIRE', KEYS[1], ARGV[1])
          ttl = tonumber(ARGV[1])
        end
        return { count, ttl }
        """;

    private readonly IConnectionMultiplexer _redis;

    public RedisRateLimitCounter(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<RateLimitCount> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken)
    {
        var result = (RedisResult[])(await _redis.GetDatabase().ScriptEvaluateAsync(
            Script, [new RedisKey(key)], [(long)window.TotalMilliseconds]))!;
        return new RateLimitCount((long)result[0], TimeSpan.FromMilliseconds((long)result[1]));
    }
}

// Without Redis (a single replica, the unit tests): the counters are kept in memory.
public sealed class InMemoryRateLimitCounter : IRateLimitCounter
{
    private readonly ConcurrentDictionary<string, (long Count, DateTimeOffset ResetsAt)> _windows = new();
    private readonly TimeProvider _timeProvider;

    public InMemoryRateLimitCounter(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public Task<RateLimitCount> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var current = _windows.AddOrUpdate(
            key,
            _ => (1, now + window),
            (_, existing) => existing.ResetsAt <= now ? (1, now + window) : (existing.Count + 1, existing.ResetsAt));
        return Task.FromResult(new RateLimitCount(current.Count, current.ResetsAt - now));
    }
}

public sealed class GatewayRateLimiter
{
    private readonly IRateLimitCounter _counter;
    private readonly ILogger<GatewayRateLimiter> _logger;
    private long _lastFailureLog;

    public GatewayRateLimiter(IRateLimitCounter counter, ILogger<GatewayRateLimiter> logger)
    {
        _counter = counter;
        _logger = logger;
    }

    // If Redis is down the request goes through: losing the limit for a while is better than refusing every call.
    public async Task<RateLimitDecision> CheckAsync(string key, int limit, TimeSpan window, CancellationToken cancellationToken)
    {
        try
        {
            var count = await _counter.IncrementAsync("ratelimit:" + key, window, cancellationToken);
            return new RateLimitDecision(count.Count <= limit, limit, Math.Max(0, limit - count.Count), count.ResetsIn);
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            var now = Environment.TickCount64;
            if (now - Interlocked.Read(ref _lastFailureLog) > 60_000)
            {
                Interlocked.Exchange(ref _lastFailureLog, now);
                _logger.LogWarning(exception, "Rate limiting is off: the Redis counters are unavailable.");
            }

            return RateLimitDecision.Unlimited(limit);
        }
    }

    public static IResult TooManyRequests(HttpContext context, RateLimitDecision decision)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling(decision.RetryAfter.TotalSeconds));
        context.Response.Headers.RetryAfter = seconds.ToString();
        return TypedResults.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            title: "Too many requests",
            detail: $"Too many requests. Try again in {seconds} seconds.");
    }
}

// Every /api call counts against its user or, without a valid token, its IP (behind nginx and Caddy, the address
// from X-Forwarded-For, see Program.cs). Runs after authentication, so a signed-in user is recognised.
public sealed class ApiRateLimitMiddleware
{
    private readonly RequestDelegate _next;

    public ApiRateLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, GatewayRateLimiter limiter, IOptions<RateLimitingOptions> options)
    {
        var settings = options.Value;
        if (!settings.Enabled || !context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var userId = context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var key = userId is not null ? $"api:user:{userId}" : $"api:ip:{context.Connection.RemoteIpAddress}";
        var decision = await limiter.CheckAsync(key, settings.PermitLimit, settings.Window, context.RequestAborted);

        context.Response.Headers["RateLimit-Limit"] = decision.Limit.ToString();
        context.Response.Headers["RateLimit-Remaining"] = decision.Remaining.ToString();

        if (!decision.Allowed)
        {
            await GatewayRateLimiter.TooManyRequests(context, decision).ExecuteAsync(context);
            return;
        }

        await _next(context);
    }
}

public static class RateLimitingExtensions
{
    public static IServiceCollection AddGatewayRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<GatewayRateLimiter>();

        var redis = configuration.GetConnectionString("redis");
        if (string.IsNullOrWhiteSpace(redis))
        {
            services.AddSingleton<IRateLimitCounter, InMemoryRateLimitCounter>();
            return services;
        }

        // Started without waiting for Redis, and failing fast while it is down, so a request never waits on it.
        var redisOptions = ConfigurationOptions.Parse(redis);
        redisOptions.AbortOnConnectFail = false;
        redisOptions.BacklogPolicy = BacklogPolicy.FailFast;
        redisOptions.ConnectTimeout = 2000;
        redisOptions.AsyncTimeout = 1000;
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisOptions));
        services.AddSingleton<IRateLimitCounter, RedisRateLimitCounter>();
        return services;
    }

    // Login attempts per account (the email in the body), on top of the limit per IP.
    public static RouteHandlerBuilder RequireLoginRateLimit(this RouteHandlerBuilder builder) =>
        builder
            .AddEndpointFilter(async (context, next) =>
            {
                var settings = context.HttpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
                var email = context.Arguments.OfType<Endpoints.AuthEndpoints.LoginBody>().FirstOrDefault()?.Email?.Trim().ToLowerInvariant();
                if (!settings.Enabled || string.IsNullOrEmpty(email))
                {
                    return await next(context);
                }

                var limiter = context.HttpContext.RequestServices.GetRequiredService<GatewayRateLimiter>();
                var decision = await limiter.CheckAsync($"login:{email}", settings.LoginPermitLimit, settings.LoginWindow, context.HttpContext.RequestAborted);
                return decision.Allowed ? await next(context) : GatewayRateLimiter.TooManyRequests(context.HttpContext, decision);
            })
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
}
