using System.Diagnostics;
using CropConnect.EndToEndTests.Infrastructure;
using static CropConnect.EndToEndTests.Infrastructure.GatewayClient;

namespace CropConnect.EndToEndTests;

// RNF availability: a failure of a non-critical service must not stop the main flow. These tests stop
// containers with Docker Compose, so they only run on demand (--filter Category=Resilience) and never
// alongside the other tests.
[Trait("Category", "Resilience")]
public sealed class ResilienceTests
{
    [Fact]
    public async Task Orders_keep_working_while_notifications_are_down()
    {
        var setup = await Scenario.FarmWithProductAsync();
        var client = await RegisterAsync("Client");

        await ComposeAsync("stop notifications-service");
        string orderId;
        try
        {
            orderId = await Scenario.PlaceOrderAsync(client, setup.ProductId);
        }
        finally
        {
            await ComposeAsync("start notifications-service");
        }

        await Eventually.GetAsync(
            () => GetAsync("api/notifications", setup.Manager.Token),
            response => response.Body?["items"]?.AsArray().Any(n => n!["relatedEntityId"]!.ToString() == orderId) == true,
            TimeSpan.FromSeconds(90),
            "the notification once Notifications is back");
    }

    [Fact]
    public async Task Emails_wait_while_the_mail_server_is_down()
    {
        var setup = await Scenario.FarmWithProductAsync();
        var client = await RegisterAsync("Client");

        await ComposeAsync("stop mailpit");
        try
        {
            var orderId = await Scenario.PlaceOrderAsync(client, setup.ProductId);
            await Eventually.GetAsync(
                () => GetAsync("api/notifications", setup.Manager.Token),
                response => response.Body?["items"]?.AsArray().Any(n => n!["relatedEntityId"]!.ToString() == orderId) == true,
                because: "the notification while the mail server is down");
            await Task.Delay(TimeSpan.FromSeconds(8));

            var notification = (await GetAsync("api/notifications", setup.Manager.Token)).Body!["items"]!.AsArray().First(n => n!["relatedEntityId"]!.ToString() == orderId)!;
            Assert.Equal("Pending", notification["emailStatus"]!.ToString());
        }
        finally
        {
            await ComposeAsync("start mailpit");
        }

        await Eventually.GetAsync(
            () => GetAsync("api/notifications", setup.Manager.Token),
            response => response.Body?["items"]?.AsArray().All(n => n!["emailStatus"]!.ToString() == "Sent") == true,
            TimeSpan.FromSeconds(90),
            "the email to be sent once the mail server is back");
    }

    private static async Task ComposeAsync(string arguments)
    {
        var start = new ProcessStartInfo("docker", $"compose {arguments}")
        {
            WorkingDirectory = RepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(start)!;
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"docker compose {arguments}: {await process.StandardError.ReadToEndAsync()}");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "compose.yaml")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("compose.yaml not found above the test output folder.");
    }
}
