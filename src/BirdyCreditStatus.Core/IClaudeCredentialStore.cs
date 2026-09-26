namespace BirdyCreditStatus.Core;

/// <summary>Naht für Credential-Ablage: Produktion liest das CLI-Login, Tests stubben.</summary>
public interface IClaudeCredentialStore
{
    Task<ClaudeOAuthCredentials?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(ClaudeOAuthCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>Hold across reloading, rotating and saving. Dispose always releases the gate.</summary>
    Task<IDisposable> AcquireRefreshLockAsync(CancellationToken cancellationToken = default) =>
        CredentialRefreshLock.AcquireStoreAsync(this, cancellationToken);
}
