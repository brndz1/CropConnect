using System.Text.Json.Nodes;
using CropConnect.ApiGateway.Infrastructure;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CropConnect.ApiGateway.Tests;

public sealed class GatewayInfrastructureTests
{
    #region gRPC status to HTTP

    [Theory]
    [InlineData(StatusCode.InvalidArgument, 400)]
    [InlineData(StatusCode.OutOfRange, 400)]
    [InlineData(StatusCode.Unauthenticated, 401)]
    [InlineData(StatusCode.PermissionDenied, 403)]
    [InlineData(StatusCode.NotFound, 404)]
    [InlineData(StatusCode.AlreadyExists, 409)]
    [InlineData(StatusCode.Aborted, 409)]
    [InlineData(StatusCode.FailedPrecondition, 422)]
    [InlineData(StatusCode.ResourceExhausted, 429)]
    [InlineData(StatusCode.Unavailable, 503)]
    [InlineData(StatusCode.DeadlineExceeded, 503)]
    [InlineData(StatusCode.Internal, 502)]
    [InlineData(StatusCode.Unknown, 502)]
    public void Grpc_status_maps_to_http_status(StatusCode code, int expected)
    {
        Assert.Equal(expected, RpcExceptionHandler.ToHttpStatus(code));
    }

    private static async Task<(bool Handled, HttpContext Context, JsonNode? Body)> HandleAsync(Exception exception)
    {
        var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = "/api/orders";
        context.Response.Body = new MemoryStream();

        var handler = new RpcExceptionHandler(services.GetRequiredService<IProblemDetailsService>(), NullLogger<RpcExceptionHandler>.Instance);
        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.Body.Position = 0;
        var text = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return (handled, context, text.Length == 0 ? null : JsonNode.Parse(text));
    }

    [Fact]
    public async Task Client_errors_keep_the_service_message()
    {
        var (handled, context, body) = await HandleAsync(new RpcException(new Status(StatusCode.FailedPrecondition, "Insufficient stock for product 'Batata'.")));

        Assert.True(handled);
        Assert.Equal(422, context.Response.StatusCode);
        Assert.Equal("Business rule violated", body!["title"]!.ToString());
        Assert.Equal("Insufficient stock for product 'Batata'.", body["detail"]!.ToString());
    }

    [Fact]
    public async Task Infrastructure_errors_hide_the_downstream_detail()
    {
        var (_, context, body) = await HandleAsync(new RpcException(new Status(StatusCode.Unavailable, "Connection refused (farms-service:8080)")));

        Assert.Equal(503, context.Response.StatusCode);
        Assert.Equal("A downstream service is unavailable.", body!["detail"]!.ToString());
    }

    [Fact]
    public async Task Other_exceptions_are_left_to_the_default_handler()
    {
        var (handled, _, body) = await HandleAsync(new InvalidOperationException());

        Assert.False(handled);
        Assert.Null(body);
    }

    #endregion

    #region Money and time

    [Theory]
    [InlineData("0")]
    [InlineData("2.5")]
    [InlineData("13.45")]
    [InlineData("999999.99")]
    [InlineData("-1.5")]
    public void Money_survives_the_round_trip_through_units_and_nanos(string text)
    {
        var value = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(value, value.ToFarmsDecimal().ToDecimal());
    }

    [Fact]
    public void Money_is_split_into_units_and_nanos()
    {
        var proto = 13.45m.ToFarmsDecimal();

        Assert.Equal(13, proto.Units);
        Assert.Equal(450_000_000, proto.Nanos);
    }

    [Fact]
    public void Missing_money_or_time_is_zero_or_null()
    {
        Assert.Equal(0m, ((Farms.Service.Protos.DecimalValue?)null).ToDecimal());
        Assert.Null(((Google.Protobuf.WellKnownTypes.Timestamp?)null).AsDateTime());
    }

    [Fact]
    public void Local_times_are_sent_as_utc()
    {
        var local = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Local);

        Assert.Equal(local.ToUniversalTime(), local.ToTimestamp().AsDateTime());
    }

    #endregion

    #region Outgoing calls

    [Fact]
    public async Task Caller_token_is_forwarded_to_the_service()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        accessor.HttpContext.Request.Headers.Authorization = "Bearer abc.def.ghi";
        var inner = new RecordingHandler();
        using var client = new HttpClient(new ForwardAuthorizationHandler(accessor) { InnerHandler = inner });

        await client.GetAsync("http://farms-service/");

        Assert.Equal("Bearer abc.def.ghi", inner.Authorization);
    }

    [Fact]
    public async Task Anonymous_calls_carry_no_token()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var inner = new RecordingHandler();
        using var client = new HttpClient(new ForwardAuthorizationHandler(accessor) { InnerHandler = inner });

        await client.GetAsync("http://farms-service/");

        Assert.Null(inner.Authorization);
    }

    [Fact]
    public void Calls_without_a_deadline_get_the_configured_timeout()
    {
        var interceptor = new DeadlineInterceptor(Options.Create(new DownstreamOptions { Timeout = TimeSpan.FromSeconds(15) }));
        var before = DateTime.UtcNow;

        var deadline = InterceptedDeadline(interceptor, new CallOptions());

        Assert.InRange(deadline!.Value, before.AddSeconds(15), DateTime.UtcNow.AddSeconds(15));
    }

    [Fact]
    public void Explicit_deadline_is_kept()
    {
        var interceptor = new DeadlineInterceptor(Options.Create(new DownstreamOptions()));
        var explicitDeadline = DateTime.UtcNow.AddSeconds(2);

        Assert.Equal(explicitDeadline, InterceptedDeadline(interceptor, new CallOptions(deadline: explicitDeadline)));
    }

    private static DateTime? InterceptedDeadline(Interceptor interceptor, CallOptions options)
    {
        var method = new Method<string, string>(MethodType.Unary, "test", "Call", Marshallers.StringMarshaller, Marshallers.StringMarshaller);
        DateTime? seen = null;

        interceptor.AsyncUnaryCall(
            "request",
            new ClientInterceptorContext<string, string>(method, null, options),
            (_, context) =>
            {
                seen = context.Options.Deadline;
                return new AsyncUnaryCall<string>(
                    Task.FromResult("response"), Task.FromResult(new Metadata()), () => Status.DefaultSuccess, () => [], () => { });
            });

        return seen;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }

    #endregion
}
