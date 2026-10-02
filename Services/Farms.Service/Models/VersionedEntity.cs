namespace Farms.Service.Models;

public abstract class VersionedEntity
{
    #region Properties

    public Guid Id { get; protected set; }

    public bool IsDeleted { get; protected set; }

    public int Version { get; protected set; }

    public DateTime CreatedAt { get; protected set; }

    public DateTime? UpdatedAt { get; protected set; }

    public abstract string EntityName { get; }

    #endregion

    #region Behaviour

    public void EnsureVersion(int? expectedVersion)
    {
        if (expectedVersion is { } expected && expected != Version)
        {
            throw new ConcurrencyException(EntityName);
        }
    }

    protected void Initialize(DateTime now)
    {
        Id = Guid.NewGuid();
        Version = 1;
        CreatedAt = now;
    }

    protected void Touch(DateTime now)
    {
        EnsureNotDeleted();
        Version++;
        UpdatedAt = now;
    }

    protected void MarkDeleted(DateTime now)
    {
        Touch(now);
        IsDeleted = true;
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
        {
            throw new BusinessRuleException($"The {EntityName} has been deleted.");
        }
    }

    #endregion
}
