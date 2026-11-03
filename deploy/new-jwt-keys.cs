// Generates the RSA key pair that signs (Auth.Service) and verifies (every other service) the JWTs, as two
// lines for an env file: the base64 of the PKCS#8 private key and of the SubjectPublicKeyInfo public key.
//   dotnet run deploy/new-jwt-keys.cs >> deploy/k8s/overlays/azure/secrets.env
using System.Security.Cryptography;

using var rsa = RSA.Create(2048);
Console.WriteLine($"JWT_PRIVATE_KEY={Convert.ToBase64String(rsa.ExportPkcs8PrivateKey())}");
Console.WriteLine($"JWT_PUBLIC_KEY={Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo())}");
