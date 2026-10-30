using Farms.Service.Application;
using Farms.Service.Models;
using Farms.Service.Services;

namespace Farms.Service.Tests.Application;

public sealed class CallerTests
{
    private static readonly DateTime Now = new(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static Product Basket(ProductType type)
    {
        var farm = Farm.Create(OwnerId, "Quinta", null, "Braga", Now);
        return Product.Create(farm, "Cabaz", null, "un", 15m, 4, type, Now.AddDays(7), Now, "Alface, tomate, cenoura");
    }

    public static TheoryData<Caller, bool> SurpriseViewers => new()
    {
        { new Caller(OwnerId, Role.Manager), true },
        { new Caller(Guid.NewGuid(), Role.Admin), true },
        { new Caller(Guid.NewGuid(), Role.Manager), false },
        { new Caller(Guid.NewGuid(), Role.Client), false },
        { Caller.Anonymous, false }
    };

    [Theory]
    [MemberData(nameof(SurpriseViewers))]
    public void Surprise_basket_contents_are_only_shown_to_who_manages_the_farm(Caller caller, bool sees)
    {
        var basket = Basket(ProductType.SurpriseBasket);

        Assert.Equal(sees, caller.CanSeeContents(basket));
        Assert.Equal(sees ? basket.Contents : string.Empty, basket.ToProto(caller).Contents);
    }

    [Fact]
    public void Predefined_basket_contents_are_public()
    {
        var basket = Basket(ProductType.PreDefinedBasket);

        Assert.Equal(basket.Contents, basket.ToProto(Caller.Anonymous).Contents);
    }

    [Fact]
    public void Only_the_owning_manager_or_an_admin_manage_a_farm()
    {
        var farm = Farm.Create(OwnerId, "Quinta", null, "Braga", Now);

        Assert.True(new Caller(OwnerId, Role.Manager).CanManage(farm));
        Assert.True(new Caller(Guid.NewGuid(), Role.Admin).CanManage(farm));
        Assert.False(new Caller(OwnerId, Role.Client).CanManage(farm));
        Assert.Throws<ForbiddenException>(() => new Caller(Guid.NewGuid(), Role.Manager).EnsureCanManage(farm));
        Assert.Throws<UnauthenticatedException>(() => Caller.Anonymous.EnsureCanManage(farm));
    }
}
