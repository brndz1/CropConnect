using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Analytics.Ingestor.Application;

namespace Analytics.Ingestor.Tests;

public sealed class PersonalDataScrubberTests
{
    private readonly PersonalDataScrubber _scrubber = RawMessageHandlerTests.Scrubber();

    [Fact]
    public void The_same_address_always_gives_the_same_hash_whatever_its_case()
    {
        Assert.Equal(_scrubber.HashEmail("ana@example.com"), _scrubber.HashEmail("  ANA@example.COM "));
        Assert.NotEqual(_scrubber.HashEmail("ana@example.com"), _scrubber.HashEmail("rui@example.com"));
        Assert.Equal(64, _scrubber.HashEmail("ana@example.com").Length);
    }

    [Fact]
    public void Another_key_gives_another_hash()
    {
        var other = new PersonalDataScrubber(Options.Create(new PseudonymizationOptions { Key = "another-pseudonymization-key-0123456789" }));

        Assert.NotEqual(_scrubber.HashEmail("ana@example.com"), other.HashEmail("ana@example.com"));
    }

    [Fact]
    public void Nested_documents_and_arrays_are_scrubbed_and_the_original_is_left_alone()
    {
        var original = BsonDocument.Parse("""{"user":{"Email":"ana@example.com","Token":"t"},"list":[{"username":"rui"}],"rating":4}""");

        var scrubbed = _scrubber.Scrub(original);

        Assert.Equal(_scrubber.HashEmail("ana@example.com"), scrubbed["user"]["emailHash"].AsString);
        Assert.False(scrubbed["user"].AsBsonDocument.Contains("Token"));
        Assert.False(scrubbed["list"][0].AsBsonDocument.Contains("username"));
        Assert.Equal(4, scrubbed["rating"].AsInt32);
        Assert.Equal("ana@example.com", original["user"]["Email"].AsString);
    }
}
