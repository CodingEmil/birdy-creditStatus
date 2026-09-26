namespace BirdyCreditStatus.Core;

/// <summary>Naht für Codex-Credential-Ablage (F002, analog <see cref="IClaudeCredentialStore"/>):
/// Produktion liest die benutzerwählte Auth-Datei, Tests stubben.</summary>
public interface ICodexCredentialStore
{
    Task<CodexOAuthCredentials?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CodexOAuthCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>Hold across reloading, rotating and saving. Dispose always releases the gate.</summary>
    Task<IDisposable> AcquireRefreshLockAsync(CancellationToken cancellationToken = default) =>
        CredentialRefreshLock.AcquireStoreAsync(this, cancellationToken);
}
