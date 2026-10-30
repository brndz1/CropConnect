using Auth.Service.Models;
using Auth.Service.Protos;
using Auth.Service.Services;
using Auth.Service.Tests.Infrastructure;
using CropConnect.Tests.Shared;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging.Abstractions;

namespace Auth.Service.Tests;

public sealed class AuthGrpcServiceTests : IDisposable
{
    private const string Password = "Passw0rd-unit";

    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private AuthGrpcService Auth() => new(
        _database.CreateContext(), TestJwt.Generator(), TestJwt.Validator(), NullLogger<AuthGrpcService>.Instance);

    private UserAccountsGrpcService Accounts() => new(_database.CreateContext(), NullLogger<UserAccountsGrpcService>.Instance);

    private async Task<Guid> RegisterAsync(string username, string role = "Client", string? email = null)
    {
        var response = await Auth().Register(
            new RegisterRequest { Username = username, Email = email ?? $"{username}@cropconnect.test", Password = Password, Role = role },
            TestServerCallContext.Anonymous());

        return Guid.Parse(response.UserId);
    }

    private async Task<Guid> SeedAdminAsync()
    {
        var admin = new User { Id = Guid.NewGuid(), Username = "admin", Email = "admin@cropconnect.test", PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password), Role = Role.Admin };
        await using var context = _database.CreateContext();
        context.Users.Add(admin);
        await context.SaveChangesAsync();
        return admin.Id;
    }

    private Task<LoginResponse> LoginAsync(string email, string password = Password) =>
        Auth().Login(new LoginRequest { Email = email, Password = password }, TestServerCallContext.Anonymous());

    #region Register and login

    [Fact]
    public async Task Registered_user_logs_in_with_a_case_insensitive_email()
    {
        var id = await RegisterAsync("ana");

        var login = await LoginAsync("ANA@CropConnect.test");

        var validated = await Auth().ValidateToken(new ValidateTokenRequest { Token = login.Token }, TestServerCallContext.Anonymous());
        Assert.True(validated.IsValid);
        Assert.Equal(id.ToString(), validated.UserId);
        Assert.Equal("Client", validated.Role);
    }

    [Fact]
    public async Task Password_is_stored_hashed()
    {
        await RegisterAsync("ana");

        await using var context = _database.CreateContext();
        var hash = context.Users.Single().PasswordHash;
        Assert.NotEqual(Password, hash);
        Assert.True(BCrypt.Net.BCrypt.Verify(Password, hash));
    }

    [Fact]
    public async Task Nobody_registers_as_administrator()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => RegisterAsync("intruder", role: "admin"));
    }

    [Fact]
    public async Task Email_and_username_are_unique()
    {
        await RegisterAsync("ana");

        await Assert.ThrowsAsync<ConflictException>(() => RegisterAsync("ana", email: "other@cropconnect.test"));
        await Assert.ThrowsAsync<ConflictException>(() => RegisterAsync("other", email: "Ana@cropconnect.test"));
    }

    [Fact]
    public async Task Wrong_password_or_unknown_email_get_the_same_answer()
    {
        await RegisterAsync("ana");

        var wrongPassword = await Assert.ThrowsAsync<UnauthenticatedException>(() => LoginAsync("ana@cropconnect.test", "Wrong-pass1"));
        var unknown = await Assert.ThrowsAsync<UnauthenticatedException>(() => LoginAsync("nobody@cropconnect.test"));

        Assert.Equal(wrongPassword.Message, unknown.Message);
    }

    [Fact]
    public async Task Deactivated_user_cannot_log_in_and_their_tokens_stop_validating()
    {
        var adminId = await SeedAdminAsync();
        var anaId = await RegisterAsync("ana");
        var token = (await LoginAsync("ana@cropconnect.test")).Token;

        await Accounts().SetUserActive(new SetUserActiveRequest { Id = anaId.ToString(), IsActive = false }, TestServerCallContext.For(adminId, "Admin"));

        await Assert.ThrowsAsync<ForbiddenException>(() => LoginAsync("ana@cropconnect.test"));
        var validated = await Auth().ValidateToken(new ValidateTokenRequest { Token = token }, TestServerCallContext.Anonymous());
        Assert.False(validated.IsValid);
    }

    #endregion

    #region Account administration

    [Fact]
    public async Task Admin_cannot_demote_or_deactivate_themself()
    {
        var adminId = await SeedAdminAsync();
        var asAdmin = TestServerCallContext.For(adminId, "Admin");

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Accounts().UpdateUser(new UpdateUserRequest { Id = adminId.ToString(), Role = "Client" }, asAdmin));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Accounts().SetUserActive(new SetUserActiveRequest { Id = adminId.ToString(), IsActive = false }, asAdmin));
    }

    [Fact]
    public async Task Users_view_only_their_own_account()
    {
        var anaId = await RegisterAsync("ana");
        var ruiId = await RegisterAsync("rui");

        var own = await Accounts().GetUser(new GetUserRequest { Id = anaId.ToString() }, TestServerCallContext.For(anaId, "Client"));
        Assert.Equal("ana", own.Username);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Accounts().GetUser(new GetUserRequest { Id = ruiId.ToString() }, TestServerCallContext.For(anaId, "Client")));
    }

    [Fact]
    public async Task Deactivated_admin_token_is_no_longer_accepted()
    {
        var adminId = await SeedAdminAsync();
        await using (var context = _database.CreateContext())
        {
            context.Users.Single().IsActive = false;
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<UnauthenticatedException>(() =>
            Accounts().CreateUser(new CreateUserRequest { Username = "rui", Email = "rui@cropconnect.test", Password = Password, Role = "Manager" }, TestServerCallContext.For(adminId, "Admin")));
    }

    #endregion

    #region Error mapping

    public static TheoryData<Exception, StatusCode> DomainErrors => new()
    {
        { new InvalidInputException("x"), StatusCode.InvalidArgument },
        { new NotFoundException("x"), StatusCode.NotFound },
        { new ForbiddenException("x"), StatusCode.PermissionDenied },
        { new UnauthenticatedException(), StatusCode.Unauthenticated },
        { new ConflictException("x"), StatusCode.AlreadyExists },
        { new BusinessRuleException("x"), StatusCode.FailedPrecondition },
        { new ConcurrencyException("user"), StatusCode.Aborted }
    };

    [Theory]
    [MemberData(nameof(DomainErrors))]
    public async Task Domain_errors_become_grpc_status_codes(Exception error, StatusCode expected)
    {
        Interceptor interceptor = new ExceptionMappingInterceptor(NullLogger<ExceptionMappingInterceptor>.Instance);

        var exception = await Assert.ThrowsAsync<RpcException>(() =>
            interceptor.UnaryServerHandler<object, object>(new object(), TestServerCallContext.Anonymous(), (_, _) => throw error));

        Assert.Equal(expected, exception.StatusCode);
        Assert.Equal(error.Message, exception.Status.Detail);
    }

    [Fact]
    public async Task Unexpected_errors_are_not_disguised_as_domain_errors()
    {
        Interceptor interceptor = new ExceptionMappingInterceptor(NullLogger<ExceptionMappingInterceptor>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            interceptor.UnaryServerHandler<object, object>(new object(), TestServerCallContext.Anonymous(), (_, _) => throw new InvalidOperationException()));
    }

    #endregion
}
