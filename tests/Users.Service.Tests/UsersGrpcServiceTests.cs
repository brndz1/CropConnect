using CropConnect.Tests.Shared;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Users.Service.Data;
using Users.Service.Models;
using Users.Service.Protos;
using Users.Service.Services;

namespace Users.Service.Tests;

public sealed class UsersGrpcServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly Guid _anaId = Guid.NewGuid();

    public UsersGrpcServiceTests()
    {
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private UsersDbContext CreateContext() => new(new DbContextOptionsBuilder<UsersDbContext>().UseSqlite(_connection).Options);

    private UsersGrpcService Service() => new(CreateContext(), NullLogger<UsersGrpcService>.Instance);

    private Task<UserProfileResponse> CreateAsync(Guid id, TestServerCallContext caller, string phone = "912 345 678") =>
        Service().CreateUserProfile(
            new CreateUserProfileRequest { Id = id.ToString(), FirstName = "  Ana ", LastName = "Silva", PhoneNumber = phone },
            caller);

    #region Access

    [Fact]
    public async Task User_creates_reads_and_updates_their_own_profile()
    {
        var ana = TestServerCallContext.For(_anaId, "Client");

        var created = await CreateAsync(_anaId, ana);
        Assert.Equal("Ana", created.FirstName);

        await Service().UpdateUserProfile(
            new UpdateUserProfileRequest { Id = _anaId.ToString(), FirstName = "Ana", LastName = "Costa", PhoneNumber = "+351912345678" }, ana);

        var read = await Service().GetUserProfile(new GetUserProfileRequest { Id = _anaId.ToString() }, ana);
        Assert.Equal("Costa", read.LastName);
        Assert.Equal("+351912345678", read.PhoneNumber);
    }

    [Fact]
    public async Task Others_cannot_touch_the_profile_but_admins_can()
    {
        await CreateAsync(_anaId, TestServerCallContext.For(_anaId, "Client"));
        var request = new GetUserProfileRequest { Id = _anaId.ToString() };

        await Assert.ThrowsAsync<ForbiddenException>(() => Service().GetUserProfile(request, TestServerCallContext.For(Guid.NewGuid(), "Manager")));
        await Assert.ThrowsAsync<ForbiddenException>(() => Service().GetUserProfile(request, TestServerCallContext.Anonymous()));

        var asAdmin = await Service().GetUserProfile(request, TestServerCallContext.For(Guid.NewGuid(), "Admin"));
        Assert.Equal("Silva", asAdmin.LastName);
    }

    [Fact]
    public async Task Profile_is_created_once()
    {
        var ana = TestServerCallContext.For(_anaId, "Client");
        await CreateAsync(_anaId, ana);

        await Assert.ThrowsAsync<ConflictException>(() => CreateAsync(_anaId, ana));
    }

    [Fact]
    public async Task Missing_profile_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Service().GetUserProfile(new GetUserProfileRequest { Id = _anaId.ToString() }, TestServerCallContext.For(_anaId, "Client")));
    }

    #endregion

    #region Input

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Profile_id_must_be_a_real_guid(string id)
    {
        Assert.Throws<InvalidInputException>(() => ProfileInput.Id(id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("912345678")]
    [InlineData("+351 912 345 678")]
    public void Valid_phone_numbers(string phone)
    {
        Assert.Equal(phone, ProfileInput.PhoneNumber(phone));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("91234567a")]
    [InlineData("+351-912-345-678")]
    [InlineData("+35191234567890123456")]
    public void Invalid_phone_numbers(string phone)
    {
        Assert.Throws<InvalidInputException>(() => ProfileInput.PhoneNumber(phone));
    }

    [Fact]
    public void Names_are_required_and_capped()
    {
        Assert.Throws<InvalidInputException>(() => ProfileInput.Name("   ", "First name"));
        Assert.Throws<InvalidInputException>(() => ProfileInput.Name(new string('a', ProfileInput.NameMaxLength + 1), "First name"));
        Assert.Equal("Ana", ProfileInput.Name(" Ana ", "First name"));
    }

    #endregion
}
