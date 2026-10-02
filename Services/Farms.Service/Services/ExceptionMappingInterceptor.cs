using Farms.Service.Models;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace Farms.Service.Services;

public sealed class ExceptionMappingInterceptor : Interceptor
{
    #region Dependencies

    private readonly ILogger<ExceptionMappingInterceptor> _logger;

    public ExceptionMappingInterceptor(ILogger<ExceptionMappingInterceptor> logger)
    {
        _logger = logger;
    }

    #endregion

    #region Interception

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (DomainException exception)
        {
            var statusCode = ToStatusCode(exception);
            _logger.LogInformation("{Method} rejected with {StatusCode}: {Reason}", context.Method, statusCode, exception.Message);
            throw new RpcException(new Status(statusCode, exception.Message));
        }
    }

    private static StatusCode ToStatusCode(DomainException exception) => exception switch
    {
        InvalidInputException => StatusCode.InvalidArgument,
        NotFoundException => StatusCode.NotFound,
        ForbiddenException => StatusCode.PermissionDenied,
        UnauthenticatedException => StatusCode.Unauthenticated,
        ConflictException => StatusCode.AlreadyExists,
        BusinessRuleException => StatusCode.FailedPrecondition,
        ConcurrencyException => StatusCode.Aborted,
        _ => StatusCode.Unknown
    };

    #endregion
}
