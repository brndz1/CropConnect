using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MongoDB.Bson;

namespace Analytics.Ingestor.Application;

public sealed class PseudonymizationOptions
{
    public const string SectionName = "Pseudonymization";

    // Secret key of the HMAC that replaces the email addresses. Without it the hashes cannot be reversed by hashing
    // a list of known addresses, and the same address always gives the same hash, so accounts can still be counted.
    [Required]
    [MinLength(32)]
    public string Key { get; set; } = string.Empty;
}

// The raw zone keeps the events as they arrived, except for personal data the analytics never need (GDPR data
// minimisation): the one-time tokens of the account links and the usernames are dropped, and the email address is
// replaced by a keyed hash (pseudonymisation).
public sealed class PersonalDataScrubber
{
    public const string EmailField = "email";
    public const string EmailHashField = "emailHash";

    private static readonly string[] RemovedFields = ["token", "username"];

    private readonly byte[] _key;

    public PersonalDataScrubber(IOptions<PseudonymizationOptions> options)
    {
        _key = Encoding.UTF8.GetBytes(options.Value.Key);
    }

    // Returns a scrubbed copy; nested documents and arrays are scrubbed too.
    public BsonDocument Scrub(BsonDocument payload)
    {
        var copy = payload.DeepClone().AsBsonDocument;
        ScrubDocument(copy);
        return copy;
    }

    public string HashEmail(string email)
    {
        var normalized = Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant());
        return Convert.ToHexStringLower(HMACSHA256.HashData(_key, normalized));
    }

    private void ScrubDocument(BsonDocument document)
    {
        foreach (var name in document.Names.ToList())
        {
            if (RemovedFields.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                document.Remove(name);
            }
            else if (string.Equals(name, EmailField, StringComparison.OrdinalIgnoreCase))
            {
                var value = document[name];
                document.Remove(name);
                document[EmailHashField] = value.IsString ? HashEmail(value.AsString) : BsonNull.Value;
            }
            else
            {
                ScrubValue(document[name]);
            }
        }
    }

    private void ScrubValue(BsonValue value)
    {
        if (value.IsBsonDocument)
        {
            ScrubDocument(value.AsBsonDocument);
        }
        else if (value.IsBsonArray)
        {
            foreach (var item in value.AsBsonArray)
            {
                ScrubValue(item);
            }
        }
    }
}
