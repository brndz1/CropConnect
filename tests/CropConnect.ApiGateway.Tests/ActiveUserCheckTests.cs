using Auth.Service.Protos;
using CropConnect.ApiGateway.Infrastructure;
using Grpc.Core;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CropConnect.ApiGateway.Tests;

public sealed class ActiveUserCheckTests : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly FakeAuth _auth = new();
    private readonly string _userId = Guid.NewGuid().ToString();

    public void Dispose() => _cache.Dispose();

    private ActiveUserCheck Check(TimeSpan? cacheDuration = null) =>
        new(_auth, _cache, Options.Create(new JwtOptions { ActiveUserCacheDuration = cacheDuration ?? TimeSpan.FromMinutes(1) }));

    [Fact]
    public async Task Active_user_passes_and_the_answer_is_cached()
    {
        _auth.Respond = request => new ValidateTokenResponse { IsValid = true, UserId = _userId, Role = "Client" };

        Assert.True(await Check().IsActiveAsync(_userId, "token", CancellationToken.None));
        Assert.True(await Check().IsActiveAsync(_userId, "token", CancellationToken.None));

        Assert.Equal(1, _auth.Calls);
    }

    [Fact]
    public async Task Deactivated_user_is_rejected()
    {
        _auth.Respond = _ => new ValidateTokenResponse { IsValid = false };

        Assert.False(await Check().IsActiveAsync(_userId, "token", CancellationToken.None));
    }

    [Fact]
    public async Task Token_of_another_user_is_rejected()
    {
        _auth.Respond = _ => new ValidateTokenResponse { IsValid = true, UserId = Guid.NewGuid().ToString() };

        Assert.False(await Check().IsActiveAsync(_userId, "token", CancellationToken.None));
    }

    [Fact]
    public async Task Deactivation_takes_effect_once_the_cache_expires()
    {
        _auth.Respond = _ => new ValidateTokenResponse { IsValid = true, UserId = _userId };
        Assert.True(await Check(TimeSpan.Zero).IsActiveAsync(_userId, "token", CancellationToken.None));

        _auth.Respond = _ => new ValidateTokenResponse { IsValid = false };
        Assert.False(await Check(TimeSpan.Zero).IsActiveAsync(_userId, "token", CancellationToken.None));
    }

    [Fact]
    public async Task Unreachable_auth_service_fails_the_request_instead_of_letting_it_through()
    {
        _auth.Respond = _ => throw new RpcException(new Status(StatusCode.Unavailable, "auth-service is down"));

        var exception = await Assert.ThrowsAsync<RpcException>(() => Check().IsActiveAsync(_userId, "token", CancellationToken.None));

        Assert.Equal(StatusCode.Unavailable, exception.StatusCode);
        Assert.Equal(503, RpcExceptionHandler.ToHttpStatus(exception.StatusCode));
    }

    private sealed class FakeAuth : AuthService.AuthServiceClient
    {
        public Func<ValidateTokenRequest, ValidateTokenResponse> Respond { get; set; } = _ => new ValidateTokenResponse();

        public int Calls { get; private set; }

        public override AsyncUnaryCall<ValidateTokenResponse> ValidateTokenAsync(ValidateTokenRequest request, CallOptions options)
        {
            Calls++;
            var response = Task.Run(() => Respond(request));
            return new AsyncUnaryCall<ValidateTokenResponse>(response, Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => [], () => { });
        }
    }
}
