using System.Net;
using CropConnect.EndToEndTests.Infrastructure;
using static CropConnect.EndToEndTests.Infrastructure.GatewayClient;

namespace CropConnect.EndToEndTests;

// RF01-RF03: registration, login, user management and profiles.
public sealed class UsersTests
{
    [Fact]
    public async Task Register_and_login_return_a_jwt()
    {
        var client = await RegisterAsync("Client");

        Assert.Equal(3, client.Token.Split('.').Length);
    }

    [Fact]
    public async Task Registering_an_existing_email_is_a_conflict()
    {
        var response = await PostAsync("api/auth/register", new { username = $"dup{Guid.NewGuid():N}"[..20], email = TestEnvironment.AdminEmail, password = "Passw0rd-e2e", role = "Client" });

        Assert.Equal(HttpStatusCode.Conflict, response.Status);
    }

    [Fact]
    public async Task Nobody_can_register_as_administrator()
    {
        var response = await PostAsync("api/auth/register", new { username = $"adm{Guid.NewGuid():N}"[..20], email = $"adm{Guid.NewGuid():N}@e2e.test", password = "Passw0rd-e2e", role = "Admin" });

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
    }

    [Fact]
    public async Task Administrator_lists_and_deactivates_users()
    {
        var admin = await AdminTokenAsync();
        var client = await RegisterAsync("Client");

        var list = await GetAsync($"api/users?search={client.Email[..20]}&pageSize=100", admin);
        Assert.Contains(list.Body!["items"]!.AsArray(), u => u!["id"]!.ToString() == client.Id);

        var deactivated = await PutAsync($"api/users/{client.Id}/active", new { isActive = false }, admin);
        Assert.False(deactivated.Body!["isActive"]!.GetValue<bool>());

        var login = await PostAsync("api/auth/login", new { email = client.Email, password = "Passw0rd-e2e" });
        Assert.NotEqual(HttpStatusCode.OK, login.Status);
    }

    [Fact]
    public async Task Clients_cannot_manage_users()
    {
        var client = await RegisterAsync("Client");

        var response = await GetAsync("api/users", client.Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
    }

    [Fact]
    public async Task User_creates_and_reads_own_profile()
    {
        var client = await RegisterAsync("Client");

        var created = await PostAsync($"api/users/{client.Id}/profile", new { firstName = "Ana", lastName = "Teste", phoneNumber = "912345678" }, client.Token);
        var read = await GetAsync($"api/users/{client.Id}/profile", client.Token);

        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal("Ana", read.Text("firstName"));
    }

    [Fact]
    public async Task User_cannot_read_someone_elses_profile()
    {
        var owner = await RegisterAsync("Client");
        var other = await RegisterAsync("Client");
        await PostAsync($"api/users/{owner.Id}/profile", new { firstName = "Rui", lastName = "Teste", phoneNumber = "912345679" }, owner.Token);

        var response = await GetAsync($"api/users/{owner.Id}/profile", other.Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
    }
}
