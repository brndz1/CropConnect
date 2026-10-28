using Orders.Service;
using Orders.Service.Data;
using Orders.Service.Security;
using Orders.Service.Services;

#region Services

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddOrdersApi(builder.Environment)
    .AddOrdersSecurity()
    .AddOrdersInfrastructure(builder.Environment)
    .AddOrdersMessaging()
    .AddOrdersApplication();

#endregion

#region Pipeline

var app = builder.Build();

await app.ApplyMigrationsIfConfiguredAsync();

app.UseAuthentication();
app.UseAuthorization();

#endregion

#region Endpoints

app.MapGrpcService<OrdersGrpcService>();
app.MapGrpcService<ReviewsGrpcService>();
app.MapDefaultEndpoints();

#endregion

await app.RunAsync();

public partial class Program
{
}
