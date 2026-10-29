using Notifications.Service;
using Notifications.Service.Data;
using Notifications.Service.Security;
using Notifications.Service.Services;

#region Services

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddNotificationsApi(builder.Environment)
    .AddNotificationsSecurity()
    .AddNotificationsInfrastructure(builder.Environment)
    .AddNotificationsMessaging()
    .AddNotificationsApplication()
    .AddNotificationsEmail(builder.Configuration);

#endregion

#region Pipeline

var app = builder.Build();

await app.ApplyMigrationsIfConfiguredAsync();

app.UseAuthentication();
app.UseAuthorization();

#endregion

#region Endpoints

app.MapGrpcService<NotificationsGrpcService>();
app.MapDefaultEndpoints();

#endregion

await app.RunAsync();

public partial class Program
{
}
