using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Options;

namespace CropConnect.ApiGateway.Infrastructure;

// Gives every downstream call a deadline, so a hung service can't hold a REST request forever.
public sealed class DeadlineInterceptor : Interceptor
{
    private readonly TimeSpan _timeout;

    public DeadlineInterceptor(IOptions<DownstreamOptions> options)
    {
        _timeout = options.Value.Timeout;
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        if (context.Options.Deadline is null)
        {
            context = new ClientInterceptorContext<TRequest, TResponse>(
                context.Method,
                context.Host,
                context.Options.WithDeadline(DateTime.UtcNow.Add(_timeout)));
        }

        return continuation(request, context);
    }
}
