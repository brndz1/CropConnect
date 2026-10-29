using Grpc.Core;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CropConnect.ApiGateway.Infrastructure;

// Turns the gRPC status a service returned into the matching HTTP status, as RFC 9457 problem details.
public sealed class RpcExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<RpcExceptionHandler> _logger;

    public RpcExceptionHandler(IProblemDetailsService problemDetails, ILogger<RpcExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not RpcException rpc)
        {
            return false;
        }

        var status = ToHttpStatus(rpc.StatusCode);

        if (status >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogWarning(rpc, "Downstream call for {Path} failed with {StatusCode}", httpContext.Request.Path, rpc.StatusCode);
        }

        httpContext.Response.StatusCode = status;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = ToTitle(status),
                // Services only put client-safe messages in the status detail; infrastructure errors are not exposed.
                Detail = status >= StatusCodes.Status500InternalServerError ? "A downstream service is unavailable." : rpc.Status.Detail
            }
        });
    }

    public static int ToHttpStatus(StatusCode code) => code switch
    {
        StatusCode.InvalidArgument or StatusCode.OutOfRange => StatusCodes.Status400BadRequest,
        StatusCode.Unauthenticated => StatusCodes.Status401Unauthorized,
        StatusCode.PermissionDenied => StatusCodes.Status403Forbidden,
        StatusCode.NotFound => StatusCodes.Status404NotFound,
        StatusCode.AlreadyExists or StatusCode.Aborted => StatusCodes.Status409Conflict,
        StatusCode.FailedPrecondition => StatusCodes.Status422UnprocessableEntity,
        StatusCode.ResourceExhausted => StatusCodes.Status429TooManyRequests,
        StatusCode.Unavailable or StatusCode.DeadlineExceeded => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status502BadGateway
    };

    private static string ToTitle(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Invalid request",
        StatusCodes.Status401Unauthorized => "Authentication required",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status422UnprocessableEntity => "Business rule violated",
        StatusCodes.Status429TooManyRequests => "Too many requests",
        StatusCodes.Status503ServiceUnavailable => "Service unavailable",
        _ => "Bad gateway"
    };
}
