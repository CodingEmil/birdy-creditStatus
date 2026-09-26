namespace BirdyCreditStatus.Core;

/// <summary>Static cards carry a provider ID; dynamic cards carry an Account record.
/// These contexts must never be conflated, even when an account is named "codex".
/// A stale account context is rejected rather than targeting its replacement.</summary>
public static class AccountMenuTarget
{
    public static string? Resolve(object? tag, IReadOnlyList<Account> accounts)
    {
        return tag switch
        {
            Account account => accounts.FirstOrDefault(a => a == account)?.Name,
            string provider when AccountProviders.IsKnown(provider) =>
                accounts.FirstOrDefault(a => a.Provider == provider)?.Name,
            _ => null,
        };
    }
}
