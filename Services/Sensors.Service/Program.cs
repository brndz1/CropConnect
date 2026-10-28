using Sensors.Service;
using Sensors.Service.Data;
using Sensors.Service.Security;
using Sensors.Service.Services;

#region Services

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddSensorsApi(builder.Environment)
    .AddSensorsSecurity()
    .AddSensorsInfrastructure(builder.Environment)
    .AddSensorsApplication();

#endregion

#region Pipeline

var app = builder.Build();

await app.ApplyMigrationsIfConfiguredAsync();

app.UseAuthentication();
app.UseAuthorization();

#endregion

#region Endpoints

app.MapGrpcService<ThresholdsGrpcService>();
app.MapDefaultEndpoints();

#endregion

await app.RunAsync();

public partial class Program
{
}
