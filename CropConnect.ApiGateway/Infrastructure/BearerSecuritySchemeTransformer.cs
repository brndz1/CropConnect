using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CropConnect.ApiGateway.Infrastructure;

// Documents the JWT bearer scheme, so the OpenAPI UI can send the token issued by POST /api/auth/login.
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public const string SchemeName = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "CropConnect API";
        document.Info.Description = "REST entry point of CropConnect. Each call is translated to gRPC and forwarded to the owning service.";

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Token returned by POST /api/auth/login."
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, document)] = []
        });

        return Task.CompletedTask;
    }
}
