var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddGrpc();

var app = builder.Build();

app.MapGet("/", () => "Orders.Service is running.");
app.MapDefaultEndpoints();

await app.RunAsync();

public partial class Program
{
}
