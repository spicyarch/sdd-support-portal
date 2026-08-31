namespace SupportPortal.Application.Abstractions;

public sealed record ProtectedSecretReference(string Version);

public sealed class ProtectedSecretStoreException : Exception
{
    public ProtectedSecretStoreException(string category, Exception? innerException = null)
        : base(category, innerException)
    {
        Category = category;
    }

    public string Category { get; }
}

public interface IProtectedSecretStore
{
    Task<string?> GetAsync(string? version, CancellationToken cancellationToken);

    Task<ProtectedSecretReference> SetAsync(string value, CancellationToken cancellationToken);
}
