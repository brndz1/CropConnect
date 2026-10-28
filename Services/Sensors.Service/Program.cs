using Sensors.Service;
using Sensors.Service.Data;

#region Services

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddGrpc();

builder.Services
    .AddSensorsInfrastructure(builder.Environment);

#endregion

#region Pipeline

var app = builder.Build();

await app.ApplyMigrationsIfConfiguredAsync();

#endregion

#region Endpoints

app.MapDefaultEndpoints();

#endregion

await app.RunAsync();

public partial class Program
{
}
