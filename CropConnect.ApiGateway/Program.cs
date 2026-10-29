using CropConnect.ApiGateway.Endpoints;
using CropConnect.ApiGateway.Infrastructure;
using Scalar.AspNetCore;

#region Services

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddGatewayApi()
    .AddGatewaySecurity()
    .AddDownstreamServices();

// Browser clients (e.g. the web front end) on other origins; none are allowed unless configured.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

#endregion

#region Pipeline

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
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
