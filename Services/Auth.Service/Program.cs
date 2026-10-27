using Microsoft.EntityFrameworkCore;
using Auth.Service.Data;
using Auth.Service.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container
builder.Services.AddGrpc(options => options.Interceptors.Add<ExceptionMappingInterceptor>());

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("AuthDb")));

builder.Services.AddScoped<JwtTokenGenerator>();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync();
}

await app.SeedBootstrapAdminAsync();

// Configure the HTTP request pipeline
app.MapGrpcService<AuthGrpcService>();
app.MapDefaultEndpoints();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

await app.RunAsync();
