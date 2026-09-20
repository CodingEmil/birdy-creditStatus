namespace BirdyCreditStatus.Core;

/// <summary>Naht für Codex-Credential-Ablage (F002, analog <see cref="IClaudeCredentialStore"/>):
/// Produktion liest die benutzerwählte Auth-Datei, Tests stubben.</summary>
public interface ICodexCredentialStore
{
    Task<CodexOAuthCredentials?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CodexOAuthCredentials credentials, CancellationToken cancellationToken = default);
}
