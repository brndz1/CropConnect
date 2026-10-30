using Farms.Service.Models;

namespace Farms.Service.Tests.Models;

public sealed class ProductTests
{
    private static readonly DateTime Now = new(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc);

    private static Farm ActiveFarm() => Farm.Create(Guid.NewGuid(), "Quinta do Vale", null, "Braga", Now);

    private static Product Basket(ProductType type, string? contents = "Alface, tomate") =>
        Product.Create(ActiveFarm(), "Cabaz", null, "un", 12m, 5, type, Now.AddDays(7), Now, contents);

    [Fact]
    public void Product_defaults_to_kilograms_and_is_available()
    {
        var product = Product.Create(ActiveFarm(), "  Batata  ", null, null, 1.20m, 30, ProductType.Product, null, Now);

        Assert.Equal("Batata", product.Name);
        Assert.Equal(Product.DefaultUnit, product.Unit);
        Assert.True(product.IsAvailable);
        Assert.True(product.IsVisibleInCatalog);
        Assert.False(product.IsBasket);
    }

    [Fact]
    public void Products_need_an_active_farm()
    {
        var farm = ActiveFarm();
        farm.SetActive(false, Now);

        Assert.Throws<BusinessRuleException>(() => Product.Create(farm, "Batata", null, null, 1m, 1, ProductType.Product, null, Now));
    }

    [Fact]
    public void Negative_stock_or_invalid_type_is_rejected()
    {
        Assert.Throws<InvalidInputException>(() => Product.Create(ActiveFarm(), "Batata", null, null, 1m, -1, ProductType.Product, null, Now));
        Assert.Throws<InvalidInputException>(() => Product.Create(ActiveFarm(), "Batata", null, null, 1m, 1, (ProductType)99, null, Now));
    }

    [Fact]
    public void Baskets_need_a_future_end_date()
    {
        Assert.Throws<InvalidInputException>(() =>
            Product.Create(ActiveFarm(), "Cabaz", null, null, 10m, 1, ProductType.PreDefinedBasket, null, Now));
        Assert.Throws<InvalidInputException>(() =>
            Product.Create(ActiveFarm(), "Cabaz", null, null, 10m, 1, ProductType.PreDefinedBasket, Now, Now));
    }

    [Fact]
    public void Only_baskets_have_an_end_date_or_contents()
    {
        Assert.Throws<InvalidInputException>(() =>
            Product.Create(ActiveFarm(), "Batata", null, null, 1m, 1, ProductType.Product, Now.AddDays(1), Now));
        Assert.Throws<InvalidInputException>(() =>
            Product.Create(ActiveFarm(), "Batata", null, null, 1m, 1, ProductType.Product, null, Now, "Batatas"));

        var product = Product.Create(ActiveFarm(), "Batata", null, null, 1m, 1, ProductType.Product, null, Now);
        Assert.Throws<BusinessRuleException>(() => product.Update(null, null, null, null, null, null, Now, contents: "x"));
        Assert.Throws<BusinessRuleException>(() => product.Update(null, null, null, null, null, Now.AddDays(1), Now));
    }

    [Fact]
    public void Only_surprise_baskets_hide_their_contents()
    {
        Assert.True(Basket(ProductType.SurpriseBasket).HidesContents);
        Assert.False(Basket(ProductType.PreDefinedBasket).HidesContents);
    }

    [Fact]
    public void Empty_contents_are_stored_as_none_and_clear_on_update()
    {
        Assert.Null(Basket(ProductType.PreDefinedBasket, "   ").Contents);

        var basket = Basket(ProductType.PreDefinedBasket);
        basket.Update(null, null, null, null, null, null, Now, contents: "");

        Assert.Null(basket.Contents);
    }

    [Fact]
    public void Basket_expires_at_its_end_date()
    {
        var basket = Basket(ProductType.SurpriseBasket);

        Assert.False(basket.IsExpired(Now.AddDays(6)));
        Assert.True(basket.IsExpired(Now.AddDays(7)));
    }

    [Fact]
    public void Expired_basket_is_final()
    {
        var basket = Basket(ProductType.SurpriseBasket);
        var afterExpiry = Now.AddDays(8);

        Assert.Throws<BusinessRuleException>(() => basket.Update(null, null, null, null, null, afterExpiry.AddDays(7), afterExpiry));
        Assert.Throws<BusinessRuleException>(() => basket.Update("Cabaz novo", null, null, null, null, null, afterExpiry));

        basket.Delete(afterExpiry);
        Assert.True(basket.IsDeleted);
    }

    [Fact]
    public void Update_changes_only_the_given_fields()
    {
        var product = Product.Create(ActiveFarm(), "Batata", "Nova", "kg", 1m, 10, ProductType.Product, null, Now);

        product.Update(null, null, null, 1.5m, false, null, Now.AddHours(1));

        Assert.Equal("Batata", product.Name);
        Assert.Equal("Nova", product.Description);
        Assert.Equal(1.5m, product.Price);
        Assert.False(product.IsAvailable);
        Assert.False(product.IsVisibleInCatalog);
        Assert.Equal(2, product.Version);
    }

    [Fact]
    public void Deleted_products_leave_the_catalog_and_cannot_change()
    {
        var product = Product.Create(ActiveFarm(), "Batata", null, null, 1m, 10, ProductType.Product, null, Now);

        product.Delete(Now);

        Assert.False(product.IsVisibleInCatalog);
        Assert.Throws<BusinessRuleException>(() => product.Update("Outro", null, null, null, null, null, Now));
    }
}
