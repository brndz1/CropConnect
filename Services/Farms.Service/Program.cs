using Farms.Service;
using Farms.Service.Services;
using Farms.Service.Data;
using Farms.Service.Security;

#region Services

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddFarmsApi(builder.Environment)
    .AddFarmsSecurity()
    .AddFarmsInfrastructure(builder.Environment)
    .AddFarmsApplication();

#endregion

#region Pipeline

var app = builder.Build();

await app.ApplyMigrationsIfConfiguredAsync();

app.UseAuthentication();
app.UseAuthorization();

#endregion

#region Endpoints

app.MapGrpcService<FarmsGrpcService>();
app.MapGrpcService<ProductsGrpcService>();
app.MapGrpcService<StockGrpcService>();
app.MapDefaultEndpoints();

#endregion

await app.RunAsync();

public partial class Program
{
}
