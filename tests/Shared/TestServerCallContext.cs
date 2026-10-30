using System.Security.Claims;
using Grpc.Core;
using Microsoft.AspNetCore.Http;

namespace CropConnect.Tests.Shared;

// A ServerCallContext for calling a gRPC service method directly, as the given user. The services read the
// user through context.GetHttpContext(), which Grpc.AspNetCore keeps in UserState under "__HttpContext".
public sealed class TestServerCallContext : ServerCallContext
{
    private readonly Dictionary<object, object> _userState = [];

    private TestServerCallContext(ClaimsPrincipal user)
    {
        _userState["__HttpContext"] = new DefaultHttpContext { User = user };
    }

    public static TestServerCallContext Anonymous() => new(new ClaimsPrincipal(new ClaimsIdentity()));

    public static TestServerCallContext For(Guid userId, string role) =>
        new(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId.ToString()), new Claim("role", role)], "Test")));

    protected override string MethodCore => "/test/Method";

    protected override string HostCore => "localhost";

    protected override string PeerCore => "ipv4:127.0.0.1:5000";

    protected override DateTime DeadlineCore => DateTime.MaxValue;

    protected override Metadata RequestHeadersCore { get; } = [];

    protected override CancellationToken CancellationTokenCore => CancellationToken.None;

    protected override Metadata ResponseTrailersCore { get; } = [];

    protected override Status StatusCore { get; set; }

    protected override WriteOptions? WriteOptionsCore { get; set; }

    protected override AuthContext AuthContextCore { get; } = new(null, []);

    protected override IDictionary<object, object> UserStateCore => _userState;

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
        throw new NotSupportedException();

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
}
