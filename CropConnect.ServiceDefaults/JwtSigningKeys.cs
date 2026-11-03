using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Extensions.Hosting;

// The JWTs are signed with RS256: Auth.Service holds the private key, the gateway and the other services only the
// public one, so they can check a token but never issue one. A key is configured as a PEM block or, to fit on one
// line of an env file, as the base64 of its DER form (PKCS#8 for the private key, SubjectPublicKeyInfo for the public).
public static class JwtSigningKeys
{
    public const int MinimumKeySize = 2048;

    // What Auth.Service signs with.
    public static RsaSecurityKey PrivateSecurityKey(string value) => SecurityKey(Import(value, isPrivate: true));

    // What every service checks the signature with.
    public static RsaSecurityKey PublicSecurityKey(string value) => SecurityKey(Import(value, isPrivate: false));

    public static bool IsValidPublicKey(string? value) => TryImport(value, isPrivate: false);

    public static bool IsValidPrivateKey(string? value) => TryImport(value, isPrivate: true);

    // The same id for both halves of the pair, written in the token header (kid) so a key can later be rotated.
    private static RsaSecurityKey SecurityKey(RSA rsa) =>
        new(rsa) { KeyId = Convert.ToHexStringLower(SHA256.HashData(rsa.ExportSubjectPublicKeyInfo()))[..16] };

    private static RSA Import(string value, bool isPrivate)
    {
        var rsa = RSA.Create();
        try
        {
            var text = value.Trim();
            if (text.StartsWith("-----BEGIN", StringComparison.Ordinal))
            {
                rsa.ImportFromPem(text);
            }
            else if (isPrivate)
            {
                rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(text), out _);
            }
            else
            {
                rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(text), out _);
            }

            if (rsa.KeySize < MinimumKeySize)
            {
                throw new CryptographicException($"The RSA key must have at least {MinimumKeySize} bits.");
            }

            return rsa;
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
    }

    private static bool TryImport(string? value, bool isPrivate)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            using var rsa = Import(value, isPrivate);
            return true;
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or ArgumentException)
        {
            return false;
        }
    }
}
