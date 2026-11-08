using Analytics.Ingestor;

// Lands the sensor readings and the domain events in MongoDB, as they arrived, for the analytics platform
// (Databricks) to read. It only listens to the other services, so none of them depends on it.

#region Services

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddIngestorStorage()
    .AddIngestorMessaging();

#endregion

#region Pipeline

var app = builder.Build();

app.MapDefaultEndpoints();

#endregion

await app.RunAsync();

public partial class Program
{
}
