namespace SupportPortal.Client.Services;

public sealed class DevelopmentIdentityState
{
    public string? IdentityKey { get; private set; }

    public event Action? Changed;

    public void SetIdentity(string identityKey)
    {
        IdentityKey = identityKey;
        Changed?.Invoke();
    }

    public void Clear()
    {
        IdentityKey = null;
        Changed?.Invoke();
    }
}