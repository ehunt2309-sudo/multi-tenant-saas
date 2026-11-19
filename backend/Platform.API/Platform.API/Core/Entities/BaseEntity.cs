namespace Platform.API.Core.Entities;

/// <summary>Root of every domain object. Guid keys prevent ID guessing and cross-tenant collisions.</summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}
