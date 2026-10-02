using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Farms.Service.Security;

public sealed class InternalApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    #region Dependencies

    public const string SchemeName = "InternalApiKey";
    public const string HeaderName = "x-api-key";
    private readonly IOptionsMonitor<InternalApiOptions> _apiOptions;

    public InternalApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptionsMonitor<InternalApiOptions> apiOptions)
        : base(options, logger, encoder)
    {
        _apiOptions = apiOptions;
    }

    #endregion

    #region Authentication

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var providedKey = Request.Headers[HeaderName].ToString();

        if (string.IsNullOrEmpty(providedKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var expected = Encoding.UTF8.GetBytes(_apiOptions.CurrentValue.ApiKey);
        var provided = Encoding.UTF8.GetBytes(providedKey);

        if (!CryptographicOperations.FixedTimeEquals(expected, provided))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "internal-service")], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    #endregion
}
