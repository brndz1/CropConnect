using System.Net;
using System.Text.Json.Nodes;
using CropConnect.EndToEndTests.Infrastructure;
using static CropConnect.EndToEndTests.Infrastructure.GatewayClient;

namespace CropConnect.EndToEndTests;

// The gateway's own behaviour: authentication at the edge, error mapping and documentation.
public sealed class GatewayTests
{
    [Fact]
    public async Task Protected_routes_require_a_valid_token()
    {
        var client = await RegisterAsync("Client");

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("api/notifications")).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("api/orders/mine", client.Token + "x")).Status);
    }

    [Fact]
    public async Task Errors_are_problem_details_with_the_matching_status()
    {
        var notFound = await GetAsync($"api/products/{Guid.NewGuid()}");
        var invalid = await GetAsync("api/products/not-an-id");

        Assert.Equal(HttpStatusCode.NotFound, notFound.Status);
        Assert.Equal("Not found", notFound.Text("title"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.Status);
        Assert.Equal(400, invalid.Body!["status"]!.GetValue<int>());
    }

    [Fact]
    public async Task OpenApi_document_describes_the_api_with_the_bearer_scheme()
    {
        var document = JsonNode.Parse(await GetStringAsync("openapi/v1.json"))!;

        Assert.True(document["paths"]!.AsObject().Count >= 30);
        Assert.NotNull(document["components"]?["securitySchemes"]?["Bearer"]);
    }

    [Fact]
    public async Task Health_endpoint_only_reports_the_status()
    {
        Assert.Equal("Healthy", await GetStringAsync("health"));
    }
}
