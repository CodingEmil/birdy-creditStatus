namespace BirdyCreditStatus.Core;

/// <summary>Naht für Credential-Ablage: Produktion liest das CLI-Login, Tests stubben.</summary>
public interface IClaudeCredentialStore
{
    Task<ClaudeOAuthCredentials?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(ClaudeOAuthCredentials credentials, CancellationToken cancellationToken = default);
}
