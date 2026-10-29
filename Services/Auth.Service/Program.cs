using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Auth.Service.Data;
using Auth.Service.Models;
using Auth.Service.Security;
using Microsoft.AspNetCore.Authentication;
using Auth.Service.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container
builder.Services.AddGrpc(options => options.Interceptors.Add<ExceptionMappingInterceptor>());

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("AuthDb")));
builder.Services.AddHealthChecks().AddCheck<AuthDbHealthCheck>("auth-db");

builder.Services.AddScoped<JwtTokenGenerator>();
builder.Services.AddSingleton<JwtTokenValidator>();

builder.Services.AddOptions<InternalApiOptions>()
    .BindConfiguration(InternalApiOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer()
    .AddScheme<AuthenticationSchemeOptions, InternalApiKeyAuthenticationHandler>(
        InternalApiKeyAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtTokenValidator>((bearer, validator) =>
    {
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = validator.CreateParameters();
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(UserAccountsGrpcService.AdminPolicy, policy => policy
        .RequireClaim(JwtTokenGenerator.RoleClaim, nameof(Role.Admin)))
    .AddPolicy(AuthorizationPolicies.InternalService, policy => policy
        .AddAuthenticationSchemes(InternalApiKeyAuthenticationHandler.SchemeName)
        .RequireAuthenticatedUser());

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync();
}

await app.SeedBootstrapAdminAsync();

// Configure the HTTP request pipeline
app.UseAuthentication();
app.UseAuthorization();

app.MapGrpcService<AuthGrpcService>();
app.MapGrpcService<UserAccountsGrpcService>();
app.MapGrpcService<UserDirectoryGrpcService>();
app.MapDefaultEndpoints();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

await app.RunAsync();
