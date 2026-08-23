using SupportPortal.Domain.Common;

namespace SupportPortal.Domain.Authorization;

public static class LastGlobalAdministratorPolicy
{
    public static void EnsureAnotherAdministratorRemains(int activeAdministratorCount, bool targetIsActiveAdministrator)
    {
        if (targetIsActiveAdministrator && activeAdministratorCount <= 1)
        {
            throw new DomainException("The final active Global Administrator cannot be removed or deactivated.");
        }
    }
}