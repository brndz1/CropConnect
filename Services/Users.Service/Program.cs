using Microsoft.EntityFrameworkCore;
using Users.Service.Data;
using Users.Service.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddGrpc();

builder.Services.AddDbContext<UsersDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("UsersDb")));

var app = builder.Build();

// Configure the HTTP request pipeline
app.MapGrpcService<UsersGrpcService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
