using Microsoft.Net.Http.Headers;

namespace CropConnect.ApiGateway.Infrastructure;

// Passes the caller's bearer token on to the service, which authorizes the call itself (RNF03):
// the gateway validates the token too, but never decides on behalf of a service.
public sealed class ForwardAuthorizationHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ForwardAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authorization = _httpContextAccessor.HttpContext?.Request.Headers[HeaderNames.Authorization].ToString();

        if (!string.IsNullOrEmpty(authorization))
        {
            request.Headers.TryAddWithoutValidation(HeaderNames.Authorization, authorization);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
