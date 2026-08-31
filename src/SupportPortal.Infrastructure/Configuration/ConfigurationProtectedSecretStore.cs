using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using SupportPortal.Application.Abstractions;

namespace SupportPortal.Infrastructure.Configuration;

public sealed class ConfigurationProtectedSecretStore : IProtectedSecretStore
{
    private readonly IConfiguration configuration;
    private readonly string secretName;
    private readonly ConcurrentDictionary<string, (string Value, string Version)> managedSecrets = new(StringComparer.Ordinal);

    public ConfigurationProtectedSecretStore(IConfiguration configuration, string secretName)
    {
        this.configuration = configuration;
        this.secretName = secretName;
    }

    public Task<string?> GetAsync(string? version, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (version is not null && managedSecrets.TryGetValue(secretName, out var managed) && managed.Version == version)
        {
            return Task.FromResult<string?>(managed.Value);
        }

        if (managedSecrets.TryGetValue(secretName, out managed))
        {
            return Task.FromResult<string?>(managed.Value);
        }

        var configured = configuration["SendGrid:ApiKey"];
        return Task.FromResult(string.IsNullOrWhiteSpace(configured) ? null : configured);
    }

    public Task<ProtectedSecretReference> SetAsync(string value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!StringComparer.OrdinalIgnoreCase.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development"))
        {
            throw new ProtectedSecretStoreException("SecretProviderUnavailable");
        }

        var version = Guid.NewGuid().ToString("N");
        managedSecrets[secretName] = (value, version);
        return Task.FromResult(new ProtectedSecretReference(version));
    }
}
