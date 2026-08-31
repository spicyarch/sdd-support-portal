using Azure;
using Azure.Security.KeyVault.Secrets;
using SupportPortal.Application.Abstractions;

namespace SupportPortal.Infrastructure.Configuration;

public sealed class KeyVaultSecretStore : IProtectedSecretStore
{
    private readonly SecretClient client;
    private readonly string secretName;

    public KeyVaultSecretStore(SecretClient client, string secretName)
    {
        this.client = client;
        this.secretName = secretName;
    }

    public async Task<string?> GetAsync(string? version, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetSecretAsync(secretName, version, cancellationToken);
            return response.Value.Value;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
        catch (RequestFailedException exception) when (exception.Status is 401 or 403)
        {
            throw new ProtectedSecretStoreException("SecretProviderUnauthorized", exception);
        }
        catch (RequestFailedException exception)
        {
            throw new ProtectedSecretStoreException("SecretProviderUnavailable", exception);
        }
        catch (Exception exception)
        {
            throw new ProtectedSecretStoreException("SecretProviderUnavailable", exception);
        }
    }

    public async Task<ProtectedSecretReference> SetAsync(string value, CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.SetSecretAsync(new KeyVaultSecret(secretName, value), cancellationToken);
            return new ProtectedSecretReference(response.Value.Properties.Version);
        }
        catch (RequestFailedException exception) when (exception.Status is 401 or 403)
        {
            throw new ProtectedSecretStoreException("SecretProviderUnauthorized", exception);
        }
        catch (RequestFailedException exception)
        {
            throw new ProtectedSecretStoreException("SecretProviderUnavailable", exception);
        }
        catch (Exception exception)
        {
            throw new ProtectedSecretStoreException("SecretProviderUnavailable", exception);
        }
    }
}
