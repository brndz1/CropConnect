using CropConnect.ApiGateway.Endpoints;
using CropConnect.ApiGateway.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;

#region Services

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddGatewayApi()
    .AddGatewaySecurity()
    .AddDownstreamServices()
    .AddGatewayRateLimiting(builder.Configuration);

// Browser clients (e.g. the web front end) on other origins; none are allowed unless configured.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

// The gateway is only reached through the web app's nginx (and Caddy on Azure), which says whether the client
// used HTTPS (the OpenAPI document needs it to give Scalar an https:// server address) and the client's IP (the rate
// limit of anonymous calls). ForwardLimit is the number of proxies in front: only the addresses they appended are
// trusted, so a client cannot pick its own IP with an X-Forwarded-For header.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
    options.ForwardLimit = builder.Configuration.GetValue("ForwardedHeaders:ForwardLimit", 1);
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

#endregion

#region Pipeline

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<ApiRateLimitMiddleware>();
app.UseAuthorization();

#endregion

#region Endpoints

// The OpenAPI document and its UI (/scalar) describe the public REST API (Tabela 9).
if (app.Configuration.GetValue("OpenApi:Enabled", true))
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("CropConnect API"));
}

app.MapAuthEndpoints();
app.MapProfileEndpoints();
app.MapFarmEndpoints();
app.MapProductEndpoints();
app.MapOrderEndpoints();
app.MapSensorEndpoints();
app.MapNotificationEndpoints();
app.MapDefaultEndpoints();

#endregion

await app.RunAsync();

public partial class Program
{
}
